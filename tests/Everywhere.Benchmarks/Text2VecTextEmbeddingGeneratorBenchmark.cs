using System.Numerics;
using BenchmarkDotNet.Attributes;
using Microsoft.KernelMemory;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Tokenizers.DotNet;
using ZLinq;

namespace Everywhere.Benchmarks;

[MaxColumn, MinColumn, MemoryDiagnoser]
public class Text2VecTextEmbeddingGeneratorBenchmark
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

    private readonly int batchSize;
    private readonly List<int> inputLengths;
    private readonly int maxLength;
    private readonly DenseTensor<long> attentionMaskTensor;
    private readonly DenseTensor<float> lastHidden; // shape: [batch, seq, hidden]

    public Text2VecTextEmbeddingGeneratorBenchmark()
    {
        var inputIds = sentences.AsValueEnumerable().Select(tokenizer.Encode).ToList();
        inputLengths = inputIds.Select(e => e.Length).ToList();
        maxLength = inputLengths.Max();

        batchSize = sentences.Length;
        var inputIdsTensor = new DenseTensor<long>(new ReadOnlySpan<int>([batchSize, maxLength]));
        attentionMaskTensor = new DenseTensor<long>(new ReadOnlySpan<int>([batchSize, maxLength]));

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

        lastHidden = outputs[0].AsTensor<float>().ToDenseTensor();
    }

    [Benchmark]
    public void Safe()
    {
        var attentionMaskSpan = attentionMaskTensor.Buffer.Span;
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

        GC.KeepAlive(embeddings);
    }

    [Benchmark]
    public void UnsafeVector()
    {
        var attentionMaskSpan = attentionMaskTensor.Buffer.Span;
        var lastHiddenSpan = lastHidden.Buffer.Span;
        var hiddenSize = lastHidden.Dimensions[2];
        var embeddings = new Embedding[batchSize];

        for (var i = 0; i < batchSize; i++)
        {
            var embedding = new float[hiddenSize];

            unsafe
            {
                fixed (float* pEmbedding = embedding)
                {
                    for (var j = 0; j < maxLength; j++)
                    {
                        var offset = i * maxLength + j;
                        if (attentionMaskSpan[offset] <= 0) continue;
                        for (var h = 0; h < hiddenSize; h++) pEmbedding[h] += lastHiddenSpan[offset * hiddenSize + h];
                    }
                }
            }

            var embeddingVector = new Vector<float>(embedding);
            embeddingVector /= inputLengths[i];

            var norm = Vector.Dot(embeddingVector, embeddingVector);
            norm = MathF.Sqrt(norm);
            if (norm > 1e-6f) embeddingVector /= norm;
            embeddingVector.CopyTo(embedding);

            embeddings[i] = new Embedding(embedding);
        }

        GC.KeepAlive(embeddings);
    }
}