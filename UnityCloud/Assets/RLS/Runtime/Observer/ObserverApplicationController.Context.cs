using UnityEngine;
using UnityEngine.UIElements;

namespace Topoda.RLS.Observer
{
    public sealed partial class ObserverApplicationController
    {
        private string selectedMemberId;
        private string memberInspectorBoundId;
        private string memberFollowingId;
        private bool memberFollowing;
        private BloomvilleMemberProxies memberProxies;

        private Label memberInspectorName;
        private Label memberInspectorRole;
        private Label memberInspectorSkill;
        private Label memberInspectorEnergy;
        private Label memberInspectorMomentum;
        private Label memberInspectorLabel;
        private Label memberInspectorNation;
        private Button memberFollowButton;

        public string SelectedMemberId { get { return selectedMemberId ?? string.Empty; } }
        public string FollowingMemberId { get { return memberFollowing ? memberFollowingId ?? string.Empty : string.Empty; } }

        private bool BuildMemberInspector(VisualElement parent)
        {
            MemberRecord member = FindMemberRecord(selectedMemberId);
            if (parent == null || member == null)
            {
                return false;
            }

            memberInspectorBoundId = member.Id;
            var panel = Box(parent, "inspector light-panel");
            memberInspectorName = Text(panel, "", "section-title");
            memberInspectorRole = Text(panel, "", "muted");
            var actions = Box(panel, "row");
            memberFollowButton = ActionButton(actions, "Follow", ToggleMemberFollowing, "primary");

            var details = new ScrollView(ScrollViewMode.Vertical);
            panel.Add(details);
            memberInspectorSkill = AddMemberMetric(details, "Skill");
            memberInspectorEnergy = AddMemberMetric(details, "Energy");
            memberInspectorMomentum = AddMemberMetric(details, "Career momentum");
            memberInspectorLabel = AddMemberMetric(details, "Label");
            memberInspectorNation = AddMemberMetric(details, "Nation");
            RefreshMemberInspector();
            return true;
        }

        private void RefreshMemberInspector()
        {
            if (string.IsNullOrEmpty(selectedMemberId) || simulation == null)
            {
                ClearMemberInspectorControls();
                return;
            }

            if (!string.Equals(memberInspectorBoundId, selectedMemberId, System.StringComparison.Ordinal))
            {
                return;
            }

            MemberRecord member = FindMemberRecord(selectedMemberId);
            if (member == null)
            {
                if (memberInspectorName != null) memberInspectorName.text = "Member unavailable";
                if (memberInspectorRole != null) memberInspectorRole.text = "This member is no longer in the current world.";
                if (memberFollowButton != null) memberFollowButton.SetEnabled(false);
                if (memberFollowing && string.Equals(memberFollowingId, selectedMemberId, System.StringComparison.Ordinal))
                {
                    memberFollowing = false;
                    memberFollowingId = null;
                }
                return;
            }

            LabelRecord label = simulation.FindLabel(member.LabelId);
            if (memberInspectorName != null) memberInspectorName.text = string.IsNullOrWhiteSpace(member.Name) ? "Unnamed member" : member.Name;
            if (memberInspectorRole != null) memberInspectorRole.text = string.IsNullOrWhiteSpace(member.Role) ? "Role not recorded" : member.Role;
            if (memberInspectorSkill != null) memberInspectorSkill.text = member.Skill.ToString("N0");
            if (memberInspectorEnergy != null) memberInspectorEnergy.text = member.Energy.ToString("N0");
            if (memberInspectorMomentum != null) memberInspectorMomentum.text = member.CareerMomentum.ToString("N0");
            if (memberInspectorLabel != null) memberInspectorLabel.text = label == null ? "Unknown label" : label.Name;
            if (memberInspectorNation != null) memberInspectorNation.text = label == null || string.IsNullOrWhiteSpace(label.Nation) ? "Not recorded" : label.Nation;
            if (memberFollowButton != null)
            {
                bool followingSelected = memberFollowing && string.Equals(memberFollowingId, selectedMemberId, System.StringComparison.Ordinal);
                memberFollowButton.text = followingSelected ? "Stop following" : "Follow";
                memberFollowButton.SetEnabled(cameraRig != null && FindMemberProxies() != null);
            }
        }

        private Label AddMemberMetric(VisualElement parent, string name)
        {
            var row = Box(parent, "metric row");
            Text(row, name, "metric-name");
            return Text(row, "—", "metric-value");
        }

        private void ToggleMemberFollowing()
        {
            if (string.IsNullOrEmpty(selectedMemberId))
            {
                return;
            }

            if (memberFollowing && string.Equals(memberFollowingId, selectedMemberId, System.StringComparison.Ordinal))
            {
                StopMemberFollowing();
                return;
            }

            BloomvilleMemberProxies proxies = FindMemberProxies();
            if (cameraRig == null || proxies == null || !proxies.TryGetMemberPosition(selectedMemberId, out Vector3 position))
            {
                RefreshMemberInspector();
                return;
            }

            memberFollowing = true;
            memberFollowingId = selectedMemberId;
            cameraRig.FollowMember(position);
            RefreshMemberInspector();
        }

        private void UpdateMemberFollowing()
        {
            if (!memberFollowing)
            {
                return;
            }

            if (state != ApplicationState.Observer || cameraRig == null || string.IsNullOrEmpty(memberFollowingId))
            {
                StopMemberFollowing();
                return;
            }

            BloomvilleMemberProxies proxies = FindMemberProxies();
            if (proxies == null || !proxies.TryGetMemberPosition(memberFollowingId, out Vector3 position))
            {
                StopMemberFollowing();
                return;
            }

            cameraRig.FollowMember(position);
        }

        private void StopMemberFollowing()
        {
            if (!memberFollowing && string.IsNullOrEmpty(memberFollowingId))
            {
                return;
            }

            memberFollowing = false;
            memberFollowingId = null;
            cameraRig?.StopFollowing();
            RefreshMemberInspector();
        }

        private void SelectMemberForInspection(string memberId)
        {
            MemberRecord member = FindMemberRecord(memberId);
            if (member == null)
            {
                return;
            }

            selectedMemberId = member.Id;
            if (!string.IsNullOrEmpty(member.LabelId))
            {
                selectedLabelId = member.LabelId;
                worldPresenter?.SetSelectedLabel(selectedLabelId);
                if (simulation != null) worldPresenter?.RefreshFromSnapshot(simulation.World);
            }

            if (memberFollowing)
            {
                memberFollowingId = member.Id;
            }
            inspectorOpen = true;
            RebuildToolkit();
        }

        private MemberRecord FindMemberRecord(string memberId)
        {
            if (simulation == null || string.IsNullOrEmpty(memberId))
            {
                return null;
            }

            for (int index = 0; index < simulation.World.Members.Count; index++)
            {
                MemberRecord member = simulation.World.Members[index];
                if (string.Equals(member.Id, memberId, System.StringComparison.Ordinal))
                {
                    return member;
                }
            }

            return null;
        }

        private BloomvilleMemberProxies FindMemberProxies()
        {
            if (memberProxies != null)
            {
                return memberProxies;
            }

            if (worldPresenter != null)
            {
                memberProxies = worldPresenter.GetComponent<BloomvilleMemberProxies>();
            }
            if (memberProxies == null)
            {
                memberProxies = FindFirstObjectByType<BloomvilleMemberProxies>();
            }
            return memberProxies;
        }

        private void ClearMemberInspectorControls()
        {
            memberInspectorBoundId = null;
            memberInspectorName = null;
            memberInspectorRole = null;
            memberInspectorSkill = null;
            memberInspectorEnergy = null;
            memberInspectorMomentum = null;
            memberInspectorLabel = null;
            memberInspectorNation = null;
            memberFollowButton = null;
        }
    }
}
