// New file. Full-screen slide dialogue system for campaign story beats.
//
// Usage:
//   CampaignDialogue.Show(CampaignDialogue.Slides.Opening, onCompleteCallback);
//
// Renders a full-screen dark overlay with a centered panel showing:
//   - Speaker name (light blue, upper area)
//   - Separator line
//   - Body text (white)
//   - Slide counter (gray, bottom-left)
//   - Continue / Close button (bottom-right)
//
// Only renders in SpaceCenter and TrackStation scenes -- never mid-flight.
// If Show() is called from another scene, it queues and fires on the next
// SC/TS visit.
//
// Voice clip hook: set DialogueSlide.audioClipPath to a GameDatabase path
// when TTS files are ready. Currently null everywhere -- system works without them.

using System;
using UnityEngine;

namespace Koko
{
    public struct DialogueSlide
    {
        public string speaker;
        public string text;
        public string audioClipPath; // null until TTS files added

        public DialogueSlide(string speaker, string text)
        {
            this.speaker       = speaker;
            this.text          = text;
            this.audioClipPath = null;
        }
    }

    // ---- Slide data -- all story beats encoded here ----
    // Each string uses \n for explicit line breaks within a slide.
    // Update speaker names or text here; no other files need changing.
    public static class CampaignSlides
    {
        public static readonly DialogueSlide[] Opening = new[]
        {
            new DialogueSlide("Gene Kerman",
                "Director...\n\nWe have a situation."),
            new DialogueSlide("Gene Kerman",
                "Three months ago, observatories in the northern hemisphere detected " +
                "unidentified transmissions originating from beyond Jool.\n\n" +
                "At first we believed them to be natural interference.\n\n" +
                "We were wrong."),
            new DialogueSlide("Gene Kerman",
                "Since then, we've detected dozens of artificial signals throughout " +
                "the Kerbol system.\n\n" +
                "Minmus.\n\nDuna.\n\nDres.\n\nEven Eeloo.\n\n" +
                "Whatever is producing these signals isn't exploring.\n\n" +
                "It's building."),
            new DialogueSlide("Gene Kerman",
                "We don't know who they are.\n\n" +
                "We don't know where they came from.\n\n" +
                "But intelligence has assigned them a designation:\n\n" +
                "The Syndicates."),
            new DialogueSlide("Gene Kerman",
                "Automated probes have photographed structures on several worlds.\n\n" +
                "Mining operations.\n\nFuel refineries.\n\nManufacturing facilities.\n\n" +
                "Supply depots."),
            new DialogueSlide("Gene Kerman",
                "They aren't conducting research.\n\n" +
                "They're constructing an economy."),
            new DialogueSlide("Gene Kerman",
                "Our analysts estimate the Syndicates established their first outposts years ago.\n\n" +
                "Hidden beneath our notice.\n\nHidden beyond our reach.\n\n" +
                "By the time we discovered them, they had already spread throughout " +
                "most of the Kerbol system."),
            new DialogueSlide("Gene Kerman",
                "Kerbin is no longer the frontier.\n\n" +
                "Kerbin is the last major world not under their control."),
            new DialogueSlide("Gene Kerman",
                "We have one advantage.\n\n" +
                "Their activity remains limited.\n\n" +
                "Most of their installations are lightly defended and poorly connected.\n\n" +
                "If we move quickly, we can isolate and destroy them before they become self-sustaining.\n\n" +
                "If we wait...\n\nWe may lose that opportunity forever."),
            new DialogueSlide("Gene Kerman",
                "Long-range surveillance indicates a massive construction project underway on Eeloo.\n\n" +
                "We don't know its purpose.\n\nWe don't know who ordered it.\n\n" +
                "We only know one thing:\n\n" +
                "Every Syndicate operation in the system appears to support it.\n\n" +
                "Whatever they're building...\n\nIt is the center of their strategy."),
            new DialogueSlide("Gene Kerman",
                "Your objectives are clear.\n\n" +
                "Locate Syndicate installations.\n\nDestroy their infrastructure.\n\n" +
                "Establish Kerbal settlements throughout the system.\n\n" +
                "Build a logistics network capable of sustaining our presence.\n\n" +
                "And discover what is being built on Eeloo.\n\n" +
                "Director...\n\nThe future of Kerbalkind depends on it.")
        };

        public static readonly DialogueSlide[] MidTwist = new[]
        {
            new DialogueSlide("Valentina Kerman",
                "Director...\n\n" +
                "We just intercepted a Syndicate communications burst from Eeloo.\n\n" +
                "We've decoded part of it."),
            new DialogueSlide("Valentina Kerman",
                "The intelligence reports were wrong."),
            new DialogueSlide("Valentina Kerman",
                "The Eeloo project isn't beginning construction.\n\n" +
                "It's nearing completion."),
            new DialogueSlide("Scientist Kerman",
                "The installations we've been destroying...\n\n" +
                "They weren't the Syndicate's core infrastructure.\n\n" +
                "They were decoys.\n\nResource feeders.\n\nDisposable assets.\n\n" +
                "We've been fighting the branches.\n\nNot the root."),
            new DialogueSlide("Valentina Kerman",
                "Every operation we discovered was one they could afford to lose.\n\n" +
                "Every facility we destroyed was already replaced somewhere else.\n\n" +
                "Somehow...\n\nThey always knew where we would look."),
            new DialogueSlide("Scientist Kerman",
                "Updated projections indicate the Eeloo structure will achieve " +
                "self-sufficiency much sooner than expected.\n\n" +
                "Once operational, it will possess enough manufacturing capacity " +
                "to build fleets faster than we can destroy them."),
            new DialogueSlide("Gene Kerman",
                "We are no longer conducting a cleanup operation.\n\n" +
                "We are racing a deadline.\n\n" +
                "Everything we've done so far has merely bought time.\n\n" +
                "Now we find Eeloo.\n\nAnd we end this.")
        };

        public static readonly DialogueSlide[] Penultimate = new[]
        {
            new DialogueSlide("Scientist Kerman",
                "Director...\n\nWe've finally determined what the Syndicates are."),
            new DialogueSlide("Scientist Kerman",
                "They aren't a government.\n\nThey aren't a species."),
            new DialogueSlide("Scientist Kerman",
                "They're a machine economy.\n\n" +
                "Every facility.\n\nEvery transport.\n\nEvery base.\n\nEvery fleet.\n\n" +
                "Autonomous.\n\nSelf-replicating."),
            new DialogueSlide("Scientist Kerman",
                "The Eeloo Fortress isn't a headquarters.\n\n" +
                "It's a factory designed to build more factories."),
            new DialogueSlide("Scientist Kerman",
                "If activated, it won't conquer Kerbin.\n\n" +
                "It won't need to.\n\n" +
                "It will simply outproduce us.")
        };

        public static readonly DialogueSlide[] Victory = new[]
        {
            new DialogueSlide("Gene Kerman",
                "It's over."),
            new DialogueSlide("Gene Kerman",
                "Across the Kerbol system, Syndicate transmissions are falling silent.\n\n" +
                "Supply routes are collapsing.\n\nFacilities are shutting down."),
            new DialogueSlide("Gene Kerman",
                "For the first time in years...\n\n" +
                "The system belongs to Kerbals again."),
            new DialogueSlide("Gene Kerman",
                "You were asked to save Kerbin.\n\n" +
                "Instead...\n\n" +
                "You saved the entire Kerbol system.")
        };

        public static readonly DialogueSlide[] Defeat = new[]
        {
            new DialogueSlide("Gene Kerman",
                "Director...\n\nThe Eeloo Fortress is operational."),
            new DialogueSlide("Gene Kerman",
                "Syndicate manufacturing capacity now exceeds anything we can produce.\n\n" +
                "They aren't invading.\n\nThey don't need to.\n\n" +
                "They're simply... growing."),
            new DialogueSlide("Gene Kerman",
                "We bought time.\n\nWe used it well.\n\nBut the clock ran out."),
            new DialogueSlide("Gene Kerman",
                "All Kerbal operations are to continue.\n\nThe mission doesn't end here.\n\n" +
                "But the war...\n\nIs over.")
        };
    }

    // ---- Dialogue renderer ----

    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class CampaignDialogue : MonoBehaviour
    {
        // Static state persists across scene transitions
        private static DialogueSlide[] _slides;
        private static int             _slideIndex;
        private static Action          _onComplete;

        public static bool IsShowing => _slides != null;

        // Expose slide sets through the renderer class for convenience
        public static class Slides
        {
            public static DialogueSlide[] Opening     => CampaignSlides.Opening;
            public static DialogueSlide[] MidTwist    => CampaignSlides.MidTwist;
            public static DialogueSlide[] Penultimate => CampaignSlides.Penultimate;
            public static DialogueSlide[] Victory     => CampaignSlides.Victory;
            public static DialogueSlide[] Defeat      => CampaignSlides.Defeat;
        }

        public static void Show(DialogueSlide[] slides, Action onComplete)
        {
            if (slides == null || slides.Length == 0) return;
            _slides      = slides;
            _slideIndex  = 0;
            _onComplete  = onComplete;
            Debug.Log($"[TimMod] CampaignDialogue: queued {slides.Length} slides.");
        }

        // Lazy-init GUI styles (must be created inside OnGUI call stack)
        private static GUIStyle _speakerStyle;
        private static GUIStyle _bodyStyle;
        private static GUIStyle _counterStyle;
        private static GUIStyle _btnStyle;

        void OnGUI()
        {
            if (_slides == null) return;

            // Render at SC, TS, and flight (opening fires at SC/TS only, but
            // mid-warp triggers can fire in flight and need immediate display)
            var scene = HighLogic.LoadedScene;
            if (scene != GameScenes.SPACECENTER &&
                scene != GameScenes.TRACKSTATION &&
                scene != GameScenes.FLIGHT) return;

            InitStyles();

            float sw = Screen.width;
            float sh = Screen.height;

            // Full-screen dim overlay
            GUI.color = new Color(0f, 0f, 0f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Centered content panel
            float panelW = Mathf.Min(660f, sw - 40f);
            float panelH = Mathf.Min(460f, sh - 40f);
            float panelX = (sw - panelW) * 0.5f;
            float panelY = (sh - panelH) * 0.5f;

            // Panel background
            GUI.color = new Color(0.04f, 0.05f, 0.09f, 0.97f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Left accent bar
            GUI.color = new Color(0.4f, 0.6f, 0.9f, 0.8f);
            GUI.DrawTexture(new Rect(panelX, panelY, 3f, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float pad  = 24f;
            float cx   = panelX + pad + 3f; // shift right of accent bar
            float cw   = panelW - pad * 2f - 3f;
            float cy   = panelY + pad;

            // Speaker name
            var slide = _slides[_slideIndex];
            GUI.Label(new Rect(cx, cy, cw, 24f), slide.speaker.ToUpper(), _speakerStyle);
            cy += 28f;

            // Separator line
            GUI.color = new Color(0.35f, 0.5f, 0.75f, 0.5f);
            GUI.DrawTexture(new Rect(cx, cy, cw, 1f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            cy += 14f;

            // Body text (fills remaining height minus button area)
            float bodyH = panelH - (cy - panelY) - 52f;
            GUI.Label(new Rect(cx, cy, cw, bodyH), slide.text, _bodyStyle);

            // Slide counter (bottom-left of panel)
            if (_slides.Length > 1)
            {
                GUI.Label(
                    new Rect(panelX + pad, panelY + panelH - 38f, 80f, 24f),
                    $"{_slideIndex + 1} / {_slides.Length}",
                    _counterStyle);
            }

            // Continue / Close button (bottom-right of panel)
            bool isLast  = _slideIndex >= _slides.Length - 1;
            string label = isLast ? "[ Close ]" : "[ Continue → ]";
            Rect btnRect = new Rect(panelX + panelW - 160f, panelY + panelH - 42f, 140f, 28f);

            if (GUI.Button(btnRect, label, _btnStyle))
            {
                if (isLast)
                    Dismiss();
                else
                    _slideIndex++;
            }
        }

        private static void Dismiss()
        {
            Action cb = _onComplete;
            _slides      = null;
            _slideIndex  = 0;
            _onComplete  = null;
            cb?.Invoke();
            Debug.Log("[TimMod] CampaignDialogue: dismissed.");
        }

        private static void InitStyles()
        {
            if (_speakerStyle != null) return;

            _speakerStyle = new GUIStyle
            {
                fontSize = 13
            };
            _speakerStyle.normal.textColor = new Color(0.55f, 0.78f, 1.0f, 1.0f); // light blue

            _bodyStyle = new GUIStyle
            {
                fontSize  = 15,
                wordWrap  = true,
                richText  = false
            };
            _bodyStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f, 1.0f); // near-white

            _counterStyle = new GUIStyle
            {
                fontSize = 11
            };
            _counterStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f, 0.85f); // gray

            _btnStyle = new GUIStyle
            {
                fontSize = 12
            };
            _btnStyle.normal.textColor  = new Color(0.7f, 0.85f, 1.0f, 0.9f);
            _btnStyle.hover.textColor   = Color.white;
            _btnStyle.active.textColor  = new Color(1.0f, 1.0f, 0.6f, 1.0f);
        }
    }
}
