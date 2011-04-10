/*==========================================================================;
 *
 *  (copyright)
 *
 *  File:          Corpus.cs
 *  Version:       1.0
 *  Desc:		   Textual corpus, XML-TEI support 
 *  Author:		   Miha Grcar
 *  Created on:    Jun-2009
 *  Last modified: Sep-2009
 *  Revision:      N/A
 *
 ***************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;
using System.Web;
using Latino;
using Latino.Model;
using Latino.TextMining;

namespace PosTagger
{
    /* .-----------------------------------------------------------------------
       |
       |  Class MoreInfo
       |
       '-----------------------------------------------------------------------
    */
    public class MoreInfo
    {
        private bool m_punct
            = false;
        private bool m_eos
            = false;
        private bool m_eop
            = false;
        private bool m_spc
            = false;

        public bool Punctuation
        {
            get { return m_punct; }
        }

        public bool EndOfSentence
        {
            get { return m_eos; }
        }

        public bool EndOfParagraph
        {
            get { return m_eop; }
        }

        public bool FollowedBySpace
        {
            get { return m_spc; }
        }

        internal void SetPunctuationFlag()
        {
            m_punct = true;
        }

        internal void SetEndOfSentenceFlag()
        {
            m_eos = true;
        }

        internal void SetEndOfParagraphFlag()
        { 
            m_eop = true;
        }

        internal void SetFollowedBySpaceFlag()
        {
            m_spc = true;
        }
    }

    /* .-----------------------------------------------------------------------
       |
       |  Class TaggedWord
       |
       '-----------------------------------------------------------------------
    */
    public class TaggedWord
    {
        private string m_word;
        private string m_tag;
        private string m_lemma;
        private MoreInfo m_more_info
            = null;
#if ERR_RPT
        private ClassifierResult<string> m_classifier_result
            = null;
#endif 

        public TaggedWord(string word, string tag, string lemma)
        {
            TaggerUtils.ThrowException(word == null ? new ArgumentNullException("word") : null);
            m_word = word;
            m_tag = tag;
            m_lemma = lemma;
        }

        public TaggedWord(string word) : this(word, /*tag=*/null, /*lemma=*/null) // throws ArgumentNullException
        {
        }

        public string WordLower
        {
            get { return m_word.ToLower(); }
        }

        public string Word
        {
            get { return m_word; }
        }

        public string Tag
        {
            get { return m_tag; }
            set { m_tag = value; }
        }

        public string Lemma
        {
            get { return m_lemma; }
            set { m_lemma = value; }
        }

        public MoreInfo MoreInfo
        {
            get { return m_more_info; }
        }

        internal void EnableMoreInfo()
        {
            m_more_info = new MoreInfo();
        }

#if ERR_RPT
        public ClassifierResult<string> ClassifierResult
        {
            get { return m_classifier_result; }
            set { m_classifier_result = value; }
        }
#endif
    }

    /* .-----------------------------------------------------------------------
       |
       |  Class Corpus
       |
       '-----------------------------------------------------------------------
    */
    public class Corpus
    {
        private ArrayList<TaggedWord> m_tagged_words 
            = new ArrayList<TaggedWord>();

        public ArrayList<TaggedWord>.ReadOnly TaggedWords
        {
            get { return m_tagged_words; }
        }

        public override string ToString()
        {
            StringBuilder str = new StringBuilder();
            foreach (TaggedWord word in m_tagged_words)
            {
                str.Append(word.Word);
                if (word.MoreInfo == null)
                {
                    str.Append(" ");
                }
                else
                {
                    if (word.MoreInfo.EndOfParagraph)
                    {
                        str.AppendLine();
                        str.AppendLine();
                    }
                    else if (word.MoreInfo.EndOfSentence)
                    {
                        str.AppendLine();
                    }
                    else if (word.MoreInfo.FollowedBySpace)
                    {
                        str.Append(" ");
                    }
                }
            }
            return str.ToString().TrimEnd(' ', '\n', '\r');
        }

        public string ToString(string format)
        {
            if (format == "T")
            {
                return ToString();
            }
            else if (format == "TT")
            {
                StringBuilder str = new StringBuilder();
                foreach (TaggedWord tagged_word in m_tagged_words)
                {
                    str.AppendLine(string.Format("{0}\t{1}", tagged_word.Word, tagged_word.Tag));
                }
                return str.ToString();
            }
            else if (format == "XML")
            {
                StringBuilder str = new StringBuilder();
                str.AppendLine("<TEI xmlns=\"http://www.tei-c.org/ns/1.0\">");
                str.AppendLine("\t<text>");
                str.AppendLine("\t\t<body>");
                str.AppendLine("\t\t\t<p>");
                bool new_sentence = true;
                foreach (TaggedWord tagged_word in m_tagged_words)
                {
                    if (new_sentence)
                    {
                        str.AppendLine("\t\t\t\t<s>");
                        new_sentence = false;
                    }
                    string tag = tagged_word.Tag;
                    bool eos = false;
                    if (tag != null && tag.EndsWith("<eos>"))
                    {
                        tag = tag.Substring(0, tag.Length - 5);
                        eos = true;
                    }
                    if (tag == tagged_word.Word)
                    {
                        str.AppendLine(string.Format("\t\t\t\t\t<c>{0}</c>", HttpUtility.HtmlEncode(tagged_word.Word)));
                    }
                    else
                    {
                        str.AppendLine(string.Format("\t\t\t\t\t<w lemma=\"{0}\" msd=\"{1}\">{2}</w>", HttpUtility.HtmlEncode(tagged_word.Lemma), tag, HttpUtility.HtmlEncode(tagged_word.Word)));
                    }
                    if (eos)
                    {
                        str.AppendLine("\t\t\t\t</s>");
                        new_sentence = true;
                    }
                }
                if (!new_sentence) { str.AppendLine("\t\t\t\t</s>"); }
                str.AppendLine("\t\t\t</p>");
                str.AppendLine("\t\t</body>");
                str.AppendLine("\t</text>");
                str.AppendLine("</TEI>");
                return str.ToString();
            }
			else if (format == "XML-MI")
            {
                StringBuilder str = new StringBuilder();
                str.AppendLine("<TEI xmlns=\"http://www.tei-c.org/ns/1.0\">");
                str.AppendLine("\t<text>");
                str.AppendLine("\t\t<body>");               
                bool new_sentence = true;
                bool new_paragraph = true;
                foreach (TaggedWord tagged_word in m_tagged_words)
                {
                    if (new_paragraph)
                    {
                        str.AppendLine("\t\t\t<p>");
                        new_paragraph = false;
                    }
                    if (new_sentence)
                    {
                        str.AppendLine("\t\t\t\t<s>");
                        new_sentence = false;
                    }
                    string tag = tagged_word.Tag;
                    if (tag != null && tag.EndsWith("<eos>"))
                    {
                        tag = tag.Substring(0, tag.Length - 5);
                    }
                    if (tagged_word.MoreInfo.Punctuation)
                    {
                        str.AppendLine(string.Format("\t\t\t\t\t<c>{0}</c>", HttpUtility.HtmlEncode(tagged_word.Word)));
                    }
                    else 
                    {
                        str.AppendLine(string.Format("\t\t\t\t\t<w lemma=\"{0}\" msd=\"{1}\">{2}</w>", HttpUtility.HtmlEncode(tagged_word.Lemma), tag, HttpUtility.HtmlEncode(tagged_word.Word)));
                    }
                    if (tagged_word.MoreInfo.FollowedBySpace)
                    {
                        str.AppendLine("\t\t\t\t\t<S/>");
                    }
                    if (tagged_word.MoreInfo.EndOfSentence)
                    {
                        str.AppendLine("\t\t\t\t</s>");
                        new_sentence = true;
                    }
                    if (tagged_word.MoreInfo.EndOfParagraph)
                    {
                        str.AppendLine("\t\t\t</p>");
                        new_paragraph = true;
                    }
                }
                if (!new_sentence) { str.AppendLine("\t\t\t\t</s>"); }
                if (!new_paragraph) { str.AppendLine("\t\t\t</p>"); }
                str.AppendLine("\t\t</body>");
                str.AppendLine("\t</text>");
                str.AppendLine("</TEI>");
                return str.ToString();
            }
            else if (format == "TBL")
            {
                StringBuilder str = new StringBuilder();
                foreach (TaggedWord tagged_word in m_tagged_words)
                {
                    str.AppendLine(string.Format("{0}\t{1}\t{2}", tagged_word.Word, tagged_word.Lemma, tagged_word.Tag));
                }
                return str.ToString();
            }
            else
            {
                throw new ArgumentNotSupportedException("format");
            }
        }

        public void LoadFromTextSsjTokenizer(string text) 
        {
            TaggerUtils.ThrowException(text == null ? new ArgumentNullException("text") : null);
            m_tagged_words.Clear();
            string xml = SsjTokenizer.Tokenize(text);
            LoadFromXml(xml, /*tag_len=*/-1);
        }
        
		public void LoadFromText(string text)
        {
            TaggerUtils.ThrowException(text == null ? new ArgumentNullException("text") : null);           
            m_tagged_words.Clear();
            RegexTokenizer tokenizer = new RegexTokenizer();
            tokenizer.TokenRegex = "[A-Za-zščžŠČŽ]+(-[A-Za-zščžŠČŽ]+)*"; 
            tokenizer.IgnoreUnknownTokens = false;
            tokenizer.Text = text;
            foreach (string word in tokenizer)
            {
                m_tagged_words.Add(new TaggedWord(word, /*tag=*/null, /*lemma=*/null));
            }
        }

        public void LoadFromFile(string file_name)
        {
            TaggerUtils.ThrowException(file_name == null ? new ArgumentNullException("file_name") : null);
            TaggerUtils.ThrowException(!Utils.VerifyFileNameOpen(file_name) ? new ArgumentValueException("file_name") : null);
            m_tagged_words.Clear();
            StreamReader reader = new StreamReader(file_name);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] tagged_word = line.Split(new string[] { " ", "\t" }, StringSplitOptions.RemoveEmptyEntries);
                if (tagged_word.Length >= 1)
                {
                    string word = tagged_word[0];
                    string tag = tagged_word.Length > 1 ? tagged_word[1] : null;
                    m_tagged_words.Add(new TaggedWord(word, tag, /*lemma=*/null));
                }
            }
        }

        public void LoadFromXml(string xml, int tag_len)
        {
            TaggerUtils.ThrowException(xml == null ? new ArgumentNullException("xml") : null);
            m_tagged_words.Clear();
            XmlTextReader xml_reader = new XmlTextReader(new StringReader(xml));
            while (xml_reader.Read())
            {
                if (xml_reader.NodeType == XmlNodeType.Element && xml_reader.Name == "p") // paragraph
                {
                    while (xml_reader.Read() && !(xml_reader.NodeType == XmlNodeType.EndElement && xml_reader.Name == "p"))
                    {
                        if (xml_reader.NodeType == XmlNodeType.Element && xml_reader.Name == "s") // sentence
                        {
                            while (xml_reader.Read() && !(xml_reader.NodeType == XmlNodeType.EndElement && xml_reader.Name == "s"))
                            {
                                if (xml_reader.NodeType == XmlNodeType.Element && xml_reader.Name == "w") // word
                                {
                                    string lemma = xml_reader.GetAttribute("lemma");
                                    string msd = xml_reader.GetAttribute("msd");
                                    if (tag_len > 0) { msd = msd.Substring(0, Math.Min(msd.Length, tag_len)).TrimEnd('-'); }
                                    xml_reader.Read();
                                    m_tagged_words.Add(new TaggedWord(xml_reader.Value, msd, lemma));
                                    m_tagged_words.Last.EnableMoreInfo();
                                }
                                if (xml_reader.NodeType == XmlNodeType.Element && xml_reader.Name == "c") // punctuation
                                {
                                    xml_reader.Read();
                                    m_tagged_words.Add(new TaggedWord(xml_reader.Value, xml_reader.Value, /*lemma=*/null));
                                    m_tagged_words.Last.EnableMoreInfo();
                                    m_tagged_words.Last.MoreInfo.SetPunctuationFlag();
                                }
                                if (xml_reader.NodeType == XmlNodeType.Element && xml_reader.Name == "S") // space
                                {
                                    if (m_tagged_words.Count > 0) { m_tagged_words.Last.MoreInfo.SetFollowedBySpaceFlag(); }
                                }
                            }
                            if (m_tagged_words.Count > 0)
                            {
                                m_tagged_words.Last.MoreInfo.SetEndOfSentenceFlag();
                                if (m_tagged_words.Last.MoreInfo.Punctuation)
                                {
                                    m_tagged_words.Last.Tag += "<eos>"; // end-of-statement punctuation                               
                                }
                            }
                        }
                    }
                    if (m_tagged_words.Count > 0) { m_tagged_words.Last.MoreInfo.SetEndOfParagraphFlag(); }
                }
            }
            xml_reader.Close();
        }

        public void LoadFromXmlFile(string file_name, int tag_len)
        {
            TaggerUtils.ThrowException(file_name == null ? new ArgumentNullException("file_name") : null);
            TaggerUtils.ThrowException(!Utils.VerifyFileNameOpen(file_name) ? new ArgumentValueException("file_name") : null);
            m_tagged_words.Clear();
            XmlTextReader xml_reader = new XmlTextReader(new FileStream(file_name, FileMode.Open));
            while (xml_reader.Read())
            {
                if (xml_reader.NodeType == XmlNodeType.Element && xml_reader.Name == "p") // paragraph
                {
                    while (xml_reader.Read() && !(xml_reader.NodeType == XmlNodeType.EndElement && xml_reader.Name == "p"))
                    {
                        if (xml_reader.NodeType == XmlNodeType.Element && xml_reader.Name == "s") // sentence
                        {
                            while (xml_reader.Read() && !(xml_reader.NodeType == XmlNodeType.EndElement && xml_reader.Name == "s"))
                            {
                                if (xml_reader.NodeType == XmlNodeType.Element && xml_reader.Name == "w") // word
                                {
                                    string lemma = xml_reader.GetAttribute("lemma");
                                    string msd = xml_reader.GetAttribute("msd");
                                    if (tag_len > 0) { msd = msd.Substring(0, Math.Min(msd.Length, tag_len)).TrimEnd('-'); }
                                    xml_reader.Read();
                                    m_tagged_words.Add(new TaggedWord(xml_reader.Value, msd, lemma));
                                    m_tagged_words.Last.EnableMoreInfo();
                                }
                                if (xml_reader.NodeType == XmlNodeType.Element && xml_reader.Name == "c") // punctuation
                                {
                                    xml_reader.Read();
                                    m_tagged_words.Add(new TaggedWord(xml_reader.Value, xml_reader.Value, /*lemma=*/null));
                                    m_tagged_words.Last.EnableMoreInfo();
                                    m_tagged_words.Last.MoreInfo.SetPunctuationFlag();
                                }
                                if (xml_reader.NodeType == XmlNodeType.Element && xml_reader.Name == "S") // space
                                {
                                    if (m_tagged_words.Count > 0) { m_tagged_words.Last.MoreInfo.SetFollowedBySpaceFlag(); }
                                }
                            }
                            if (m_tagged_words.Count > 0) 
                            { 
                                m_tagged_words.Last.MoreInfo.SetEndOfSentenceFlag();
                                if (m_tagged_words.Last.MoreInfo.Punctuation)
                                {                                    
                                    m_tagged_words.Last.Tag += "<eos>"; // end-of-statement punctuation                               
                                }
                            }
                        }                        
                    }
                    if (m_tagged_words.Count > 0) { m_tagged_words.Last.MoreInfo.SetEndOfParagraphFlag(); }
                }
            }
            xml_reader.Close();
        }

        public Corpus[] Split(int num_folds, Random rnd)
        {
            TaggerUtils.ThrowException(m_tagged_words.Count == 0 ? new InvalidOperationException() : null);
            TaggerUtils.ThrowException(num_folds < 2 ? new ArgumentOutOfRangeException("num_folds") : null);
            ArrayList<IdxDat<int>> sentences = new ArrayList<IdxDat<int>>();
            // index sentences            
            int idx = 0;
            int len = 0;
            foreach (TaggedWord word in m_tagged_words)
            {
                TaggerUtils.ThrowException(word.MoreInfo == null ? new InvalidOperationException() : null);
                len++;
                if (word.MoreInfo.EndOfSentence)
                { 
                    sentences.Add(new IdxDat<int>(idx, len));
                    idx += len;
                    len = 0;
                }
            }
            // throw exceptions
            TaggerUtils.ThrowException(sentences.Count < 2 ? new InvalidOperationException() : null);
            TaggerUtils.ThrowException(num_folds > sentences.Count ? new ArgumentOutOfRangeException("num_folds") : null);            
            // shuffle and split sentences
            sentences.Shuffle(rnd);
            Corpus[] folds = new Corpus[num_folds];
            for (int i = 0; i < num_folds; i++)
            {
                folds[i] = new Corpus();
            }
            for (int i = 0; i < sentences.Count; i++)
            {
                idx = i % num_folds;
                IdxDat<int> sentence = sentences[i];
                for (int j = sentence.Idx; j < sentence.Idx + sentence.Dat; j++)
                {
                    folds[idx].m_tagged_words.Add(m_tagged_words[j]);
                }
            }            
            return folds;
        }

        public void AddCorpus(Corpus corpus)
        {
            foreach (TaggedWord word in corpus.m_tagged_words)
            {
                m_tagged_words.Add(word);
            }
        }

        public void SaveIntoFile(string file_name)
        {
            TaggerUtils.ThrowException(file_name == null ? new ArgumentNullException("file_name") : null);
            TaggerUtils.ThrowException(!Utils.VerifyFileNameCreate(file_name) ? new ArgumentValueException("file_name") : null);
            StreamWriter writer = new StreamWriter(file_name);
            foreach (TaggedWord tagged_word in m_tagged_words)
            {
                writer.WriteLine("{0}\t{1}", tagged_word.Word, tagged_word.Tag);
            }
            writer.Close();
        }

        private static void AddFeature(string feature_name, Dictionary<string, int> feature_space, bool extend_feature_space, ArrayList<int> feature_vector)
        {
            if (feature_space.ContainsKey(feature_name))
            {
                feature_vector.Add(feature_space[feature_name]);
            }
            else if (extend_feature_space)
            {
                feature_vector.Add(feature_space.Count);
                feature_space.Add(feature_name, feature_space.Count);
            }
        }

        private static string GetSuffix(string word, int n)
        {
            if (word.Length <= n) { return word; }
            return word.Substring(word.Length - n);
        }

        private static string GetPrefix(string word, int n)
        {
            if (word.Length <= n) { return word; }
            return word.Substring(0, n);
        }

        public BinaryVector<int> GenerateFeatureVector(int word_idx, Dictionary<string, int> feature_space, bool extend_feature_space, SuffixTrie suffix_trie)
        {
            TaggerUtils.ThrowException((word_idx < 0 || word_idx >= m_tagged_words.Count) ? new ArgumentOutOfRangeException("word_idx") : null);
            TaggerUtils.ThrowException(suffix_trie == null ? new ArgumentNullException("suffix_trie") : null);
            ArrayList<int> feature_vector = new ArrayList<int>();
            for (int offset = -3; offset <= 3; offset++) // consider context of 3 + 1 + 3 words
            {
                int idx = word_idx + offset;
                // *** unigrams ***
                if (idx >= 0 && idx < m_tagged_words.Count)
                {
                    AddFeature(string.Format("w({0}) {1}", offset, m_tagged_words[idx].WordLower), feature_space, extend_feature_space, feature_vector);
                    for (int i = 1; i <= 4; i++) // consider prefixes and suffixes of up to 4 letters
                    {
                        string prefix = GetPrefix(m_tagged_words[idx].WordLower, i);
                        AddFeature(string.Format("p{0}({1}) {2}", i, offset, prefix), feature_space, extend_feature_space, feature_vector);
                        string suffix = GetSuffix(m_tagged_words[idx].WordLower, i);
                        AddFeature(string.Format("s{0}({1}) {2}", i, offset, suffix), feature_space, extend_feature_space, feature_vector);
                    }
                    if (offset < 0) // tag is available iff offset < 0
                    {
                        AddFeature(string.Format("t({0}) {1}", offset, m_tagged_words[idx].Tag), feature_space, extend_feature_space, feature_vector);
                        if (m_tagged_words[idx].Tag.Length > 0)
                        {
                            AddFeature(string.Format("t1({0}) {1}", offset, m_tagged_words[idx].Tag[0]), feature_space, extend_feature_space, feature_vector);
                        }
                    }
                    else // tag not available; use "maybe" features and ambiguity class instead
                    {
                        string word = m_tagged_words[idx].WordLower;
                        Set<string>.ReadOnly tags = suffix_trie.GetTags(word);
                        foreach (string tag in tags)
                        {
                            AddFeature(string.Format("m({0}) {1}", offset, tag), feature_space, extend_feature_space, feature_vector);
                            if (tag.Length > 0)
                            {
                                AddFeature(string.Format("m1({0}) {1}", offset, tag[0]), feature_space, extend_feature_space, feature_vector);
                            }
                        }
                        string ambiguity_class = suffix_trie.GetAmbiguityClass(word);
                        AddFeature(string.Format("t({0}) {1}", offset, ambiguity_class), feature_space, extend_feature_space, feature_vector);
                    }
                }
            }
#if NGRAM_FEATURES
            // *** bigrams and trigrams ***
            for (int n = 2; n <= 3; n++)
            {
                for (int offset = -2; offset <= 3 - n; offset++) // consider 4 bigrams and 3 trigrams
                {
                    string word_feature = string.Format("w({0},{1})", n, offset);
                    string tag_feature = string.Format("t({0},{1})", n, offset);
                    string[] prefix_feature = new string[4];
                    string[] suffix_feature = new string[4];
                    for (int i = 0; i < 4; i++) // consider prefixes and suffixes of up to 4 letters
                    {
                        prefix_feature[i] = string.Format("p{0}({1},{2})", i, n, offset);
                        suffix_feature[i] = string.Format("s{0}({1},{2})", i, n, offset);
                    }
                    if (word_idx + offset >= 0 && word_idx + offset + (n - 1) < m_tagged_words.Count)
                    {
                        for (int i = 0; i < n; i++)
                        {
                            int idx = word_idx + offset + i;
                            string word = m_tagged_words[idx].WordLower;
                            word_feature += " " + word;
                            for (int j = 0; j < 4; j++) // prefixes and suffixes
                            {
                                prefix_feature[j] += " " + GetPrefix(word, j);
                                suffix_feature[j] += " " + GetSuffix(word, j);
                            }
                            if (offset + i < 0) // tag is available iff offset + i < 0
                            {
                                tag_feature += " " + m_tagged_words[idx].Tag;
                            }
                            else // tag not available; use ambiguity class instead
                            {
                                string ambiguity_class = suffix_trie.GetAmbiguityClass(word);
                                tag_feature += " " + ambiguity_class;
                            }
                        }
                        AddFeature(word_feature, feature_space, extend_feature_space, feature_vector);
                        AddFeature(tag_feature, feature_space, extend_feature_space, feature_vector);
                        for (int i = 0; i < 4; i++) // add prefix and suffix features
                        {
                            AddFeature(prefix_feature[i], feature_space, extend_feature_space, feature_vector);
                            AddFeature(suffix_feature[i], feature_space, extend_feature_space, feature_vector);
                        }
                    }
                }
            }
#endif
            // character features
            foreach (char ch in m_tagged_words[word_idx].Word)
            {
                // contains non-alphanum char?
                if (!char.IsLetterOrDigit(ch))
                {
                    AddFeature(string.Format("c{0}", ch), feature_space, extend_feature_space, feature_vector);
                }
                // contains number?
                if (char.IsDigit(ch))
                {
                    AddFeature("cd", feature_space, extend_feature_space, feature_vector);
                }
                // contains uppercase char?
                if (char.IsUpper(ch))
                {
                    AddFeature("cu", feature_space, extend_feature_space, feature_vector);
                }
            } 
            // starts with capital letter?
            if (m_tagged_words[word_idx].Word.Length > 0 && char.IsUpper(m_tagged_words[word_idx].Word[0]))
            {
                AddFeature("cl", feature_space, extend_feature_space, feature_vector);
            }
            // starts with capital letter and not first word?
            if (word_idx > 0 && !m_tagged_words[word_idx - 1].Tag.EndsWith("<eos>") && m_tagged_words[word_idx].Word.Length > 0 && char.IsUpper(m_tagged_words[word_idx].Word[0]))
            {
                AddFeature("cl+", feature_space, extend_feature_space, feature_vector);
            }
            return new BinaryVector<int>(feature_vector);
        }

        private Set<string> GetHiddenWords(int num_folds)
        {
            Set<string> hidden_words = new Set<string>();
            Set<string>[] folds = new Set<string>[num_folds];
            for (int i = 0; i < folds.Length; i++) { folds[i] = new Set<string>(); }
            int words_per_fold = (int)Math.Floor((double)m_tagged_words.Count / (double)num_folds);
            for (int i = 0; i < m_tagged_words.Count; i++)
            {
                int fold_idx = Math.Min((int)Math.Floor((double)i / (double)words_per_fold), folds.Length - 1);
                folds[fold_idx].Add(m_tagged_words[i].WordLower);
            }
            for (int i = 0; i < folds.Length; i++)
            {
                foreach (string word in folds[i])
                {
                    bool is_hidden_word = true;
                    for (int j = 0; j < folds.Length; j++)
                    {
                        if (i != j)
                        {
                            if (folds[j].Contains(word))
                            {
                                is_hidden_word = false;
                                break;
                            }
                        }
                    }
                    if (is_hidden_word)
                    {
                        hidden_words.Add(word);
                    }
                }
            }
            return hidden_words;
        }
    }
}
