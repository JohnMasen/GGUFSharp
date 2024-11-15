namespace GGUFSharp.Test
{
    [TestClass]
    public sealed class BasicFeatureTest
    {
        private string GGUFFilePath = @"D:\MyModels\Phi35GGUF\Phi-3.5-mini-instruct-IQ2_M.gguf";
        [TestMethod]
        public void TestMethod1()
        {
            GGUFReader reader = new GGUFReader();
            reader.Read(GGUFFilePath);
        }
    }
}
