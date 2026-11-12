using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ContextStage
{
    internal static class StoryEnglish
    {
        static Dictionary<string, List<string>> _linesBySequence;

        public static string Line(DialogueSequence sequence, int index)
        {
            string original = sequence.Lines[index].text ?? string.Empty;
            if (!GameLanguage.English) return original;
            if (_linesBySequence == null) _linesBySequence = Load();
            return _linesBySequence.TryGetValue(sequence.SequenceId, out var lines)
                && lines.Count == sequence.LineCount ? lines[index] : original;
        }

        public static string Speaker(DialogueLine line) => !GameLanguage.English ? line.speakerName : line.speakerId switch
        {
            "raccoon" => "Raccoon",
            "hedgehog" => "Hedgehog",
            "skunk" => "Skunk",
            "rival" => "LUX//FAUNA",
            _ => line.speakerName,
        };

        // Keep the source sequence IDs and line counts in sync. A missing or mismatched
        // section falls back to the original sequence instead of shifting speakers/cues.
        static Dictionary<string, List<string>> Load()
        {
            var sections = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>("Localization/Story.en");
            if (asset == null) return sections;

            using var reader = new StringReader(asset.text);
            List<string> section = null;
            for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    section = new List<string>();
                    sections.Add(line.Substring(1, line.Length - 2), section);
                }
                else
                    section?.Add(line.Replace("\\n", "\n"));
            }
            return sections;
        }

        public static (string title, string body) StageGuide(string stageId, bool poster) => stageId switch
        {
            "stage_01" => ("Alley Busking",
                "Bored fans will leave. Check their outfits and energy, then play a card that fits."),
            "stage_02" => ("Special Requests",
                "Drop a matching SPECIAL card directly onto the fan who requested it."),
            "stage_03" => ("Battle for the Crowd",
                "Play cards that suit fans heading for the rival stage. Watch for surprise events!"),
            "stage_04" => ("Live Broadcast",
                "Your performance triggers one boost or setback. Play a card that matches the cue."),
            "stage_05_boss" => ("Rival Battle", poster
                ? "Raise the featured fan's energy to 60 or land a matching SPECIAL card to win them over."
                : "LUX//FAUNA tries to steal fans. Raise the featured fan's energy to 60 or hit them with a matching SPECIAL card."),
            _ => (null, null),
        };

        public static string EndingTitle(TourEndingKind ending, string original) => GameLanguage.Pick(original, ending switch
        {
            TourEndingKind.Bad => "Show Cut Short",
            TourEndingKind.AllS => "S Rank at Every Show",
            TourEndingKind.Mosh => "Crowd Surfing",
            TourEndingKind.Singalong => "Singalong",
            TourEndingKind.Chill => "First Single",
            _ => original,
        });

        public static string EndingSummary(TourEndingKind ending, string original) => GameLanguage.Pick(original, ending switch
        {
            TourEndingKind.Bad => "They told us to pack up our gear. Total score: {0}.",
            TourEndingKind.AllS => "The stadium screen is already advertising the next tour. Total score: {0}.",
            TourEndingKind.Mosh => "The basement crowd wouldn't back down. Total score: {0}. Top fans: {1}.",
            TourEndingKind.Singalong => "You can still hear the chorus outside. Total score: {0}. Top fans: {1}.",
            TourEndingKind.Chill => "The track we recorded made it onto a featured playlist. Total score: {0}. Top fans: {1}.",
            _ => original,
        });
    }
}
