using System.Web.Services;
using System.Threading;
using PosTagger;

[WebService(Namespace = "http://tempuri.org/")]
[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
public class Service : WebService
{
    [WebMethod]
    public bool Ready()
    {
        return Global.mReady;
    }

    [WebMethod]
    public string Tag(string text)
    {
        while (!Global.mReady) { Thread.Sleep(100); }
        Corpus corpus = new Corpus();
        corpus.LoadFromTextSsjTokenizer(text);
        int lemmaCorrect, lemmaCorrectLowercase, lemmaWords;
        Global.mPosTagger.Tag(corpus, out lemmaCorrect, out lemmaCorrectLowercase, out lemmaWords, /*xmlMode=*/false);
        return corpus.ToString("XML-MI");
    }
}
