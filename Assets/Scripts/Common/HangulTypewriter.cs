using System.Collections.Generic;
using System.Text;

namespace ContextStage
{
    /// <summary>타이핑 연출 한 프레임. Text 는 그 시점에 보여줄 전체 문자열이다.</summary>
    public readonly struct TypewriterFrame
    {
        public TypewriterFrame(string text, float extraDelay)
        {
            Text = text;
            ExtraDelay = extraDelay;
        }

        public string Text { get; }
        public float ExtraDelay { get; }
    }

    /// <summary>
    /// 한글 자모 조립 타이핑 프레임 생성기. (관객 Hover 말풍선과 대화창이 공유한다)
    ///
    /// - 완성형 한글 한 글자는 초성 → 초성+중성 → 완성 순으로 세 프레임을 만든다
    /// - &lt;color&gt; 같은 리치 텍스트 태그는 한 번에 통째로 붙여 태그가 깨진 채 보이지 않게 한다
    /// - 문장 부호 뒤에는 punctuationDelay 만큼 잠깐 멈춘다
    /// </summary>
    public static class HangulTypewriter
    {
        static readonly char[] CompatibilityInitials =
        {
            'ㄱ', 'ㄲ', 'ㄴ', 'ㄷ', 'ㄸ', 'ㄹ', 'ㅁ', 'ㅂ', 'ㅃ', 'ㅅ',
            'ㅆ', 'ㅇ', 'ㅈ', 'ㅉ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ',
        };

        public static List<TypewriterFrame> BuildFrames(string richText, float punctuationDelay)
        {
            richText = richText ?? string.Empty;
            var frames = new List<TypewriterFrame>(richText.Length * 2 + 1);
            var committed = new StringBuilder(richText.Length + 16);

            for (int i = 0; i < richText.Length; i++)
            {
                char value = richText[i];
                if (value == '<')
                {
                    int tagEnd = richText.IndexOf('>', i);
                    if (tagEnd >= i)
                    {
                        committed.Append(richText, i, tagEnd - i + 1);
                        i = tagEnd;
                        continue;
                    }
                }

                if (TryDecomposeHangul(value, out char initial, out char medialForm, out bool hasFinal))
                {
                    frames.Add(new TypewriterFrame(committed.ToString() + initial, 0f));
                    frames.Add(new TypewriterFrame(committed.ToString() + medialForm, 0f));
                    if (hasFinal)
                        frames.Add(new TypewriterFrame(committed.ToString() + value, 0f));
                    committed.Append(value);
                    continue;
                }

                committed.Append(value);
                if (char.IsWhiteSpace(value)) continue;

                float delay = IsPunctuation(value) ? punctuationDelay : 0f;
                frames.Add(new TypewriterFrame(committed.ToString(), delay));
            }

            if (frames.Count == 0 || frames[frames.Count - 1].Text != richText)
                frames.Add(new TypewriterFrame(richText, 0f));
            return frames;
        }

        public static bool TryDecomposeHangul(
            char value,
            out char initial,
            out char medialForm,
            out bool hasFinal)
        {
            const int hangulBase = 0xAC00;
            const int hangulLast = 0xD7A3;
            int code = value;
            if (code < hangulBase || code > hangulLast)
            {
                initial = default;
                medialForm = default;
                hasFinal = false;
                return false;
            }

            int syllableIndex = code - hangulBase;
            int initialIndex = syllableIndex / 588;
            int medialIndex = (syllableIndex % 588) / 28;
            int finalIndex = syllableIndex % 28;

            initial = CompatibilityInitials[initialIndex];
            medialForm = (char)(hangulBase + initialIndex * 588 + medialIndex * 28);
            hasFinal = finalIndex > 0;
            return true;
        }

        public static bool IsPunctuation(char value) =>
            value == '.' || value == ',' || value == '!' || value == '?' ||
            value == '…' || value == '。' || value == '！' || value == '？';
    }
}
