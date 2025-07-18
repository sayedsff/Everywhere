using Everywhere.Chat;
using Everywhere.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.Configuration;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Tokenizers.DotNet;
using ZLinq;

namespace Everywhere.Tests;

public class Text2Vec
{
    private readonly Tokenizer tokenizer = new(@".\Assets\text2vec-chinese-base\tokenizer.json");
    private readonly InferenceSession session = new(@".\Assets\text2vec-chinese-base\model.onnx");
    private readonly string[] sentences =
    [
        "张飞的武器是丈八蛇矛",
        "修复当服务无响应时，无法结束的bug",
        "海内存知己天涯若比邻",
        "黑神话获得了金摇杆奖",
        "三国演义有哪些武器"
    ];
    private readonly float[] groundTruth = [0.202f, 0.150f, 0.197f, 0.399f];

    [Test]
    public void SimilarityTest()
    {
        var inputIds = sentences.AsValueEnumerable().Select(tokenizer.Encode).ToList();
        var inputLengths = inputIds.Select(e => e.Length).ToList();
        var maxLength = inputLengths.Max();

        var batchSize = sentences.Length;
        var inputIdsTensor = new DenseTensor<long>(new ReadOnlySpan<int>([batchSize, maxLength]));
        var attentionMaskTensor = new DenseTensor<long>(new ReadOnlySpan<int>([batchSize, maxLength]));

        var inputIdsSpan = inputIdsTensor.Buffer.Span;
        var attentionMaskSpan = attentionMaskTensor.Buffer.Span;
        for (var i = 0; i < batchSize; i++)
        {
            var inputId = inputIds[i];
            var offset0 = i * maxLength;
            for (var j = 0; j < maxLength; j++)
            {
                var offset = offset0 + j;
                if (j < inputId.Length)
                {
                    inputIdsSpan[offset] = inputId[j];
                    attentionMaskSpan[offset] = 1; // Attention mask for valid tokens
                }
                else
                {
                    inputIdsSpan[offset] = 0; // Padding
                    attentionMaskSpan[offset] = 0; // Padding
                }
            }
        }

        var tokenTypeIdsTensor = new DenseTensor<long>(new ReadOnlySpan<int>([batchSize, maxLength]));
        tokenTypeIdsTensor.Fill(0);

        using var outputs = session.Run(
        [
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor),
        ]);

        var lastHidden = outputs[0].AsTensor<float>().ToDenseTensor(); // shape: [batch, seq, hidden]
        var lastHiddenSpan = lastHidden.Buffer.Span;
        var hiddenSize = lastHidden.Dimensions[2];
        var embeddings = new Embedding[batchSize];

        for (var i = 0; i < batchSize; i++)
        {
            var embedding = new float[hiddenSize];

            float sumMask = 0;
            for (var j = 0; j < maxLength; j++)
            {
                var offset = i * maxLength + j;
                if (attentionMaskSpan[offset] <= 0) continue;
                sumMask += 1;

                for (var h = 0; h < hiddenSize; h++) embedding[h] += lastHiddenSpan[offset * hiddenSize + h];
            }

            for (var h = 0; h < hiddenSize; h++) embedding[h] /= sumMask;

            var norm = 0f;
            for (var h = 0; h < hiddenSize; h++) norm += embedding[h] * embedding[h];
            norm = MathF.Sqrt(norm);
            for (var h = 0; h < hiddenSize; h++) embedding[h] /= norm;

            embeddings[i] = new Embedding(embedding);
        }

        for (var i = 1; i < batchSize; i++)
        {
            var embedding = embeddings[i];
            var similarity = embedding.CosineSimilarity(embeddings[0]);
            Console.WriteLine($"Sentence: {sentences[i]}, Similarity: {similarity}, Ground Truth: {groundTruth[i - 1]}");
            Assert.That(similarity, Is.EqualTo(groundTruth[i - 1]).Within(0.01));
        }
    }

    [Test]
    [TestCase("gpt-4o")]
    public async Task KnowledgeBaseTest(string modelId)
    {
        var apiKey = Environment
            .GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User)
            .NotNull("To run this test, please set OPENAI_API_KEY environment variable.");
        var memory = new KernelMemoryBuilder()
            .Configure(builder =>
            {
                var baseFolder = Path.Combine(
                    Environment.CurrentDirectory,
                    "Assets",
                    "text2vec-chinese-base");
                var generator = new Text2VecTextEmbeddingGenerator(
                    Path.Combine(baseFolder, "tokenizer.json"),
                    Path.Combine(baseFolder, "model.onnx"));
                builder.AddSingleton<ITextEmbeddingGenerator>(generator);
                builder.AddSingleton<ITextEmbeddingBatchGenerator>(generator);
                builder.AddIngestionEmbeddingGenerator(generator);
                builder.Services.AddSingleton<ITextGenerator>(new OpenAIKernelMixin(modelId, "https://api.openai.com/v1", apiKey));
                builder.AddSingleton(
                    new TextPartitioningOptions
                    {
                        MaxTokensPerParagraph = generator.MaxTokens,
                        OverlappingTokens = generator.MaxTokens / 20
                    });
            })
            .Configure(builder => builder.Services.AddLogging(l => l.AddSimpleConsole()))
            .Build<MemoryServerless>();

        foreach (var filePath in Directory.EnumerateFiles(Path.Combine(Environment.CurrentDirectory, "Assets", "KnowledgeBase")))
        {
            await memory.ImportDocumentAsync(filePath);
        }

        var response = await memory.AskAsync("罗秉初有哪些著作？");
        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.Contain("机器伦理的建构路径与边界思考"));
            Assert.That(response.Result, Does.Contain("人机共识框架中的价值映射难题"));
            Assert.That(response.Result, Does.Contain("智能代理的伦理维度：模型构建与风险预控"));
        });

        response = await memory.AskAsync("How about Critical intervention accuracy？");
        Assert.That(response.Result, Does.Contain("96.4%"));
    }
}