using System;
using System.IO;
using Latino;
using LemmaSharp;
using PosTagger;

namespace PosTagger
{
    class Program
    {
        static bool mVerbose
            = false;

        static void OutputHelp()
        {
            Console.WriteLine("*** Lematizator 1.0 - Modul za učenje ***");
            Console.WriteLine();
            Console.WriteLine("Uporaba:");
            Console.WriteLine("LemmatizerTrain [<nastavitve>] <korpus_xml> <model_bin>");
            Console.WriteLine();
            Console.WriteLine("<nastavitve>:  Glej spodaj.");
            Console.WriteLine("<korpus_xml>:  Učni korpus v formatu XML-TEI (vhod).");
            Console.WriteLine("<model_bin>:   Model za lematizacijo (izhod).");
            Console.WriteLine();
            Console.WriteLine("Nastavitve:");
            Console.WriteLine("-v              Izpisovanje na zaslon (verbose).");
            Console.WriteLine("                (privzeto: ni izpisovanja)");
            Console.WriteLine("-t              Upoštevanje oblikoslovnih oznak.");
            Console.WriteLine("                (privzeto: oblikoslovne oznake niso upoštevane)");
            Console.WriteLine("-o              Optimizacija lematizacijskega drevesa (oznake SSJ).");
            Console.WriteLine("                (privzeto: optimizacija se ne izvede)");
            Console.WriteLine("-l:ime_datoteke Uporaba leksikona.");
            Console.WriteLine("                (privzeto: učenje brez leksikona)");
        }

        static bool ParseParams(string[] args, ref bool verbose, ref bool considerTag, ref bool treeOpt, ref string corpusFileName, ref string modelFileName,
            ref string lexiconFileName)
        {
            // parse
            for (int i = 0; i < args.Length - 2; i++)
            {
                string argLwr = args[i].ToLower();
                if (argLwr == "-v")
                {
                    verbose = true;
                }
                else if (argLwr == "-t")
                {
                    considerTag = true;
                }
                else if (argLwr == "-o")
                {
                    treeOpt = true;
                }
                else if (argLwr.StartsWith("-l:"))
                {
                    lexiconFileName = args[i].Substring(3, args[i].Length - 3);
                }
                else
                {
                    Console.WriteLine("*** Napačna nastavitev {0}.\r\n", args[i]);
                    OutputHelp();
                    return false;
                }
            }
            // check file names
            corpusFileName = args[args.Length - 2];
            modelFileName = args[args.Length - 1];
            if (lexiconFileName != null && !Utils.VerifyFileNameOpen(lexiconFileName))
            {
                Console.WriteLine("*** Napačno ime datoteke ali datoteka ne obstaja ({0}).\r\n", lexiconFileName);
                OutputHelp();
                return false;
            }
            if (!Utils.VerifyFileNameOpen(corpusFileName))
            {
                Console.WriteLine("*** Napačno ime datoteke ali datoteka ne obstaja ({0}).\r\n", corpusFileName);
                OutputHelp();
                return false;
            }
            if (!Utils.VerifyFileNameCreate(modelFileName))
            {
                Console.WriteLine("*** Napačno ime izhodne datoteke ({0}).\r\n", modelFileName);
                OutputHelp();
                return false;
            }
            return true;
        }

        static void Verbose(string text, params object[] args)
        {
            if (mVerbose)
            {
                Console.Write(text, args);
            }
        }

        static void Main(string[] args)
        {
            try
            {
                if (args.Length < 2)
                {
                    OutputHelp();
                }   
                else
                {
                    //StreamWriter w = new StreamWriter(@"c:\zamatjaza.txt");
                    string corpusFileName = null, modelFileName = null, lexiconFileName = null;
                    bool considerTag = false;
                    bool treeOpt = false;
                    if (ParseParams(args, ref mVerbose, ref considerTag, ref treeOpt, ref corpusFileName, ref modelFileName, ref lexiconFileName))
                    {
                        Corpus corpus = new Corpus();
                        Verbose("Gradim model za lematizacijo ...\r\n");
                        Verbose("Nalagam učni korpus ...\r\n");
                        corpus.LoadFromXmlFile(corpusFileName, /*tagLen=*/int.MaxValue);                        
                        LemmatizerSettings lemmatizerSettings = new LemmatizerSettings();
                        lemmatizerSettings.eMsdConsider = considerTag ? LemmatizerSettings.MsdConsideration.Distinct : LemmatizerSettings.MsdConsideration.Ignore;
                        lemmatizerSettings.bUseFromInRules = true;
                        lemmatizerSettings.iMaxRulesPerNode = 0;
                        lemmatizerSettings.bBuildFrontLemmatizer = false;
                        lemmatizerSettings.bStoreAllFullKnownWords = false;
                        lemmatizerSettings.bUseMsdSplitTreeOptimization = treeOpt;
                        Lemmatizer lemmatizer = new Lemmatizer(lemmatizerSettings);     
                        for (int i = 0; i < corpus.TaggedWords.Count; i++)
                        {
                            Verbose("{0} / {1}\r", i + 1, corpus.TaggedWords.Count);
                            TaggedWord word = corpus.TaggedWords[i];
                            if (!word.MoreInfo.Punctuation)
                            {
                                lemmatizer.AddExample(word.Word, word.Lemma, 1, word.Tag);
                                //w.WriteLine("{0}\t{1}\t{2}\t{3}", word.Word, word.Lemma, 1, word.Tag);
                            }
                        }
                        //w.Close();
                        Verbose("\r\n");                        
                        if (lexiconFileName != null)
                        { 
                            Console.Write("Nalagam leksikon ...\r\n");
                            StreamReader lexReader = new StreamReader(lexiconFileName);
                            string lexLine;
                            int i = 0;
                            while ((lexLine = lexReader.ReadLine()) != null)
                            {
                                // lexicon format: word \t lemma \t tag \t freq
                                Verbose("{0}\r", ++i);
                                string[] lexData = lexLine.Split('\t');
                                string word = lexData[0];
                                string lemma = lexData[1];
                                string tag = lexData[2];
                                double freq = Math.Max(0.1, Convert.ToDouble(lexData[3]));
                                lemmatizer.AddExample(word, lemma, freq, tag);
                            }
                            lexReader.Close();
                            Verbose("\r\n");      
                        }
                        if (treeOpt)
                        {
                            string msdSpec = Utils.GetManifestResourceString(typeof(Program), "MsdSpecsSloSloCodes.txt");
                            MsdSplitTree.BeamSearchParams beamSearchParams = new MsdSplitTree.BeamSearchParams();
                            beamSearchParams.beamsPerLevel[0] = 2;
                            lemmatizer.BuildModel(msdSpec, beamSearchParams);
                        }
                        else
                        {
                            lemmatizer.BuildModel();
                        }
                        Verbose("Zapisujem model ...\r\n");
                        BinarySerializer writer = new BinarySerializer(modelFileName, FileMode.Create);
                        writer.WriteBool(considerTag);
                        lemmatizer.Save(writer);
                        writer.Close();
                        Verbose("Končano.\r\n");
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("*** Nepričakovana napaka. Podrobnosti: {0}\r\n{1}", exception, exception.StackTrace);
            }
        }
    }
}
