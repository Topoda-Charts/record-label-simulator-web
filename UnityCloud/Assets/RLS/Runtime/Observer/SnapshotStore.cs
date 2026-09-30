using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace Topoda.RLS.Observer
{
    public sealed class SnapshotStore
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void RlsSyncIdbfs();
#endif
        private readonly string rootDirectory;
        private readonly int recoveryCount;

        public SnapshotStore(string directory, int rotatingRecoveryCount)
        {
            rootDirectory = directory;
            recoveryCount = Math.Max(2, rotatingRecoveryCount);
        }

        public string RootDirectory { get { return rootDirectory; } }

        public SnapshotMetadata SaveAutosave(WorldSnapshot world, string reason)
        {
            Directory.CreateDirectory(rootDirectory);
            SnapshotMetadata saved = Write(world, "Autosave — " + reason, "autosave.json", true);
            string previous = Path.Combine(rootDirectory, "autosave.json.previous");
            if (File.Exists(previous))
            {
                for (int index = recoveryCount - 1; index >= 1; index--)
                {
                    string source = Path.Combine(rootDirectory, "autosave-" + index + ".json");
                    string target = Path.Combine(rootDirectory, "autosave-" + (index + 1) + ".json");
                    if (File.Exists(source)) File.Copy(source, target, true);
                }
                File.Copy(previous, Path.Combine(rootDirectory, "autosave-1.json"), true);
                File.Delete(previous);
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            RlsSyncIdbfs();
#endif
            return saved;
        }

        public SnapshotMetadata SaveNamed(WorldSnapshot world, string requestedName)
        {
            string displayName = string.IsNullOrWhiteSpace(requestedName) ? "Snapshot" : requestedName.Trim();
            string safe = SanitizeFileName(displayName);
            if (safe.Length == 0)
            {
                safe = "Snapshot";
            }

            if (IsReservedWindowsName(safe)) safe = "Snapshot-" + safe;
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string fileName = "snapshot-" + safe + "-" + stamp + ".json";
            int duplicate = 2;
            while (File.Exists(Path.Combine(rootDirectory, fileName))) fileName = "snapshot-" + safe + "-" + stamp + "-" + duplicate++ + ".json";
            SnapshotMetadata saved = Write(world, displayName, fileName, false);
#if UNITY_WEBGL && !UNITY_EDITOR
            RlsSyncIdbfs();
#endif
            return saved;
        }

        public SnapshotEnvelope Load(string fileName, int expectedSchemaVersion)
        {
            string fullPath = ResolveInsideRoot(fileName);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("That snapshot is no longer available.", fullPath);
            }

            SnapshotEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<SnapshotEnvelope>(File.ReadAllText(fullPath));
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("That snapshot could not be read. Your other saves were not changed.", exception);
            }

            ValidateEnvelope(envelope, expectedSchemaVersion);
            return envelope;
        }

        public List<SnapshotMetadata> List()
        {
            var result = new List<SnapshotMetadata>();
            if (!Directory.Exists(rootDirectory))
            {
                return result;
            }

            foreach (string file in Directory.GetFiles(rootDirectory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                try
                {
                    SnapshotEnvelope envelope = JsonUtility.FromJson<SnapshotEnvelope>(File.ReadAllText(file));
                    if (envelope == null || envelope.Metadata == null)
                    {
                        continue;
                    }

                    envelope.Metadata.FileName = Path.GetFileName(file);
                    envelope.Metadata.IntegrityValid = envelope.World != null && string.Equals(DeterminismDigest.Calculate(envelope.World), envelope.Metadata.Digest, StringComparison.OrdinalIgnoreCase);
                    envelope.Metadata.Compatibility = envelope.Metadata.IntegrityValid ? "Ready" : "Needs attention";
                    result.Add(envelope.Metadata);
                }
                catch
                {
                    result.Add(new SnapshotMetadata
                    {
                        Id = Path.GetFileNameWithoutExtension(file),
                        DisplayName = Path.GetFileName(file),
                        FileName = Path.GetFileName(file),
                        IntegrityValid = false,
                        Compatibility = "Unreadable — current world remains safe"
                    });
                }
            }
            return result;
        }

        public static string Encode(WorldSnapshot world, string displayName)
        {
            world.Digest = DeterminismDigest.Calculate(world);
            var envelope = new SnapshotEnvelope
            {
                Metadata = new SnapshotMetadata
                {
                    Id = "memory",
                    DisplayName = displayName,
                    SavedAtUtcTicks = DateTime.UtcNow.Ticks,
                    SimulationTicks = world.Clock.CurrentTicks,
                    Seed = world.Seed,
                    SchemaVersion = world.SchemaVersion,
                    TuningVersion = world.TuningVersion,
                    Digest = world.Digest,
                    IntegrityValid = true,
                    Compatibility = "Compatible"
                },
                World = world
            };
            return JsonUtility.ToJson(envelope, false);
        }

        public static SnapshotEnvelope Decode(string json, int expectedSchemaVersion)
        {
            SnapshotEnvelope envelope = JsonUtility.FromJson<SnapshotEnvelope>(json);
            if (envelope == null || envelope.World == null || envelope.Metadata == null)
            {
                throw new InvalidDataException("Snapshot data is incomplete.");
            }
            if (envelope.World.SchemaVersion != expectedSchemaVersion)
            {
                throw new InvalidDataException("Snapshot schema is incompatible.");
            }
            if (!string.Equals(envelope.World.TuningVersion, SimulationTuning.ObserverPreview().Version, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Snapshot tuning is incompatible. Silent migration is not allowed.");
            }
            string digest = DeterminismDigest.Calculate(envelope.World);
            if (digest != envelope.World.Digest || digest != envelope.Metadata.Digest)
            {
                throw new InvalidDataException("Snapshot integrity check failed.");
            }
            return envelope;
        }

        private static void ValidateEnvelope(SnapshotEnvelope envelope, int expectedSchemaVersion)
        {
            if (envelope == null || envelope.World == null || envelope.Metadata == null)
            {
                throw new InvalidDataException("Snapshot data is incomplete.");
            }
            if (envelope.World.SchemaVersion != expectedSchemaVersion)
            {
                throw new InvalidDataException("Snapshot schema is incompatible.");
            }
            if (!string.Equals(envelope.World.TuningVersion, SimulationTuning.ObserverPreview().Version, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Snapshot tuning is incompatible. Silent migration is not allowed.");
            }
            string digest = DeterminismDigest.Calculate(envelope.World);
            if (digest != envelope.World.Digest || digest != envelope.Metadata.Digest)
            {
                throw new InvalidDataException("Snapshot integrity check failed.");
            }
        }

        private SnapshotMetadata Write(WorldSnapshot world, string displayName, string fileName, bool autosave)
        {
            Directory.CreateDirectory(rootDirectory);
            world.Digest = DeterminismDigest.Calculate(world);
            var metadata = new SnapshotMetadata
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = displayName,
                FileName = fileName,
                SavedAtUtcTicks = DateTime.UtcNow.Ticks,
                SimulationTicks = world.Clock.CurrentTicks,
                Seed = world.Seed,
                SchemaVersion = world.SchemaVersion,
                TuningVersion = world.TuningVersion,
                Digest = world.Digest,
                IsAutosave = autosave,
                IntegrityValid = true,
                Compatibility = "Ready"
            };
            var envelope = new SnapshotEnvelope { Metadata = metadata, World = world };
            string json = JsonUtility.ToJson(envelope, false);
            string finalPath = ResolveInsideRoot(fileName);
            string temporaryPath = finalPath + ".writing";
            string previousPath = finalPath + ".previous";

            File.WriteAllText(temporaryPath, json);
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                VerifySnapshotFile(temporaryPath, metadata.Digest);
                bool hadPreviousSave = File.Exists(finalPath);
                if (hadPreviousSave)
                {
                    File.Copy(finalPath, previousPath, true);
                }

                try
                {
                    if (hadPreviousSave)
                    {
                        File.Copy(temporaryPath, finalPath, true);
                    }
                    else
                    {
                        File.Move(temporaryPath, finalPath);
                    }
                    VerifySnapshotFile(finalPath, metadata.Digest);
                }
                catch (Exception exception)
                {
                    try
                    {
                        if (hadPreviousSave && File.Exists(previousPath))
                        {
                            File.Copy(previousPath, finalPath, true);
                        }
                        else if (File.Exists(finalPath))
                        {
                            File.Delete(finalPath);
                        }
                        RlsSyncIdbfs();
                    }
                    catch
                    {
                        // The staged copy remains at .previous when restoring the destination fails.
                    }
                    throw new IOException("The new save could not be committed. The previous recovery copy was kept where possible.", exception);
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
                catch
                {
                    // A leftover staging file is ignored by the snapshot catalog and can be removed on a later save.
                }
            }
#else
            SnapshotEnvelope verification = JsonUtility.FromJson<SnapshotEnvelope>(File.ReadAllText(temporaryPath));
            if (verification == null || verification.World == null || DeterminismDigest.Calculate(verification.World) != metadata.Digest)
            {
                File.Delete(temporaryPath);
                throw new IOException("The new save did not pass verification, so the previous valid save was kept.");
            }

            if (File.Exists(finalPath)) File.Replace(temporaryPath, finalPath, previousPath, true);
            else File.Move(temporaryPath, finalPath);
#endif
            return metadata;
        }

        private static void VerifySnapshotFile(string filePath, string expectedDigest)
        {
            SnapshotEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<SnapshotEnvelope>(File.ReadAllText(filePath));
            }
            catch (Exception exception)
            {
                throw new IOException("The staged save could not be read back.", exception);
            }

            if (envelope == null || envelope.World == null || envelope.Metadata == null)
            {
                throw new IOException("The staged save is incomplete.");
            }
            string digest = DeterminismDigest.Calculate(envelope.World);
            if (!string.Equals(digest, expectedDigest, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(envelope.World.Digest, expectedDigest, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(envelope.Metadata.Digest, expectedDigest, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("The staged save did not pass its digest check.");
            }
        }

        private string ResolveInsideRoot(string fileName)
        {
            string root = Path.GetFullPath(rootDirectory) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(rootDirectory, Path.GetFileName(fileName)));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The requested snapshot path is outside the local save folder.");
            }
            return candidate;
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(value.Where(character => !invalid.Contains(character) && !char.IsControl(character)).Take(48).ToArray()).Trim();
        }

        private static bool IsReservedWindowsName(string value)
        {
            string stem = value.Split('.')[0].Trim().ToUpperInvariant();
            string[] reserved = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            return reserved.Contains(stem);
        }
    }
}
