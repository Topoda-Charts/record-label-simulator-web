const { onRequest } = require("firebase-functions/v2/https");
const admin = require("firebase-admin");

admin.initializeApp();

const KEYS = ["label_title", "tempo", "quality"];

function validatePayload(body) {
  if (!body || typeof body !== "object") {
    return "Missing JSON body";
  }
  const { label_title, tempo, quality } = body;
  if (!label_title || typeof label_title !== "string" || label_title.length > 120) {
    return "Invalid label_title";
  }
  const tempoNum = Number(tempo);
  const qualityNum = Number(quality);
  if (!Number.isFinite(tempoNum) || tempoNum < 40 || tempoNum > 240) {
    return "Invalid tempo";
  }
  if (!Number.isFinite(qualityNum) || qualityNum < 0 || qualityNum > 100) {
    return "Invalid quality";
  }
  return null;
}

exports.publishRemoteConfig = onRequest({ cors: true, region: "us-central1" }, async (req, res) => {
  if (req.method === "OPTIONS") {
    res.status(204).send("");
    return;
  }
  if (req.method !== "POST") {
    res.status(405).json({ error: "Method not allowed" });
    return;
  }

  const validationError = validatePayload(req.body);
  if (validationError) {
    res.status(400).json({ error: validationError });
    return;
  }

  const { label_title, tempo, quality } = req.body;

  try {
    const rc = admin.remoteConfig();
    const template = await rc.getTemplate();

    template.parameters.label_title = {
      defaultValue: { value: String(label_title) },
      valueType: "STRING",
    };
    template.parameters.tempo = {
      defaultValue: { value: String(tempo) },
      valueType: "STRING",
    };
    template.parameters.quality = {
      defaultValue: { value: String(quality) },
      valueType: "STRING",
    };

    for (const key of KEYS) {
      if (!template.parameters[key]) {
        res.status(500).json({ error: `Remote Config parameter missing: ${key}` });
        return;
      }
    }

    await rc.publishTemplate(template);
    res.json({ ok: true, updated: KEYS });
  } catch (err) {
    res.status(500).json({ error: err.message || "Publish failed" });
  }
});
