using System;

namespace Gita.Data
{
    /// <summary>One of the eighteen chapters (yogas) of the Bhagavad Gita.</summary>
    [Serializable]
    public class Chapter
    {
        public int number;       // 1..18
        public string name;      // Devanagari title, e.g. अर्जुनविषादयोग
        public string translit;  // IAST, e.g. Arjun Viṣhād Yog
        public string nameEn;    // Romanised, e.g. Arjuna Visada Yoga
        public string meaning;   // e.g. Arjuna's Dilemma
        public string summary;   // English chapter summary
        public string summaryHi; // Hindi chapter summary
        public int verseCount;
    }

    /// <summary>
    /// A single translation of the whole text, by one translator into one language.
    /// The reader picks a language first, then optionally a translator within it.
    /// </summary>
    [Serializable]
    public class Edition
    {
        public string id;         // stable key, e.g. "en-sivananda"
        public string lang;       // BCP-47 subtag, e.g. "en"
        public string langLabel;  // shown in the picker, in that language
        public string translator; // shown beneath the verse
        public string tts;        // locale for Android TextToSpeech, e.g. "en-IN"
        public bool poetic;       // the literary rendering for this language, if any
    }

    /// <summary>A single shloka. Sanskrit and transliteration never vary by language.</summary>
    [Serializable]
    public class Verse
    {
        public int c;        // chapter number
        public int v;        // verse number within the chapter
        public string sa;    // Sanskrit, Devanagari
        public string tr;    // IAST transliteration
        public string wm;    // word-by-word meanings
        public string[] t;   // one entry per edition, parallel to GitaDatabase.Editions

        public string Reference => $"{c}.{v}";
    }

    [Serializable]
    public class GitaPayload
    {
        public Edition[] editions;
        public Chapter[] chapters;
        public Verse[] verses;
    }

    /// <summary>Which layer of the text the reader is currently showing.</summary>
    public enum TextLayer { Translation, Sanskrit, Transliteration, WordByWord }
}
