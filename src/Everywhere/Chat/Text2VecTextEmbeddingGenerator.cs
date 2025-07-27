// using Microsoft.KernelMemory;
// using Microsoft.KernelMemory.AI;
// using Microsoft.ML.OnnxRuntime;
// using Microsoft.ML.OnnxRuntime.Tensors;
// using Tokenizers.DotNet;
// using ZLinq;
//
// namespace Everywhere.Chat;
//
// public class Text2VecTextEmbeddingGenerator(string vocabPath, string embeddingModelPath) : ITextEmbeddingGenerator, ITextEmbeddingBatchGenerator
// {
//     private readonly Tokenizer tokenizer = new(vocabPath);
//     private readonly InferenceSession session = new(embeddingModelPath);
//
//     public int MaxTokens => 512;
//
//     public int MaxBatchSize => 8;
//
//     public int CountTokens(string text)
//     {
//         return tokenizer.Encode(text).Length;
//     }
//
//     public IReadOnlyList<string> GetTokens(string text)
//     {
//         return tokenizer.Encode(text).AsValueEnumerable().Select(id => id.ToString()).ToImmutableList();
//     }
//
//     public async Task<Embedding> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = new())
//     {
//         var results = await GenerateEmbeddingBatchAsync([text], cancellationToken);
//         return results[0];
//     }
//
//     public Task<Embedding[]> GenerateEmbeddingBatchAsync(IEnumerable<string> textList, CancellationToken cancellationToken = new())
//     {
//         return Task.Run(() =>
//             {
//                 var sentences = textList.AsValueEnumerable().ToImmutableList();
//                 var inputIds = sentences.AsValueEnumerable().Select(tokenizer.Encode).ToList();
//                 var inputLengths = inputIds.Select(e => e.Length).ToList();
//                 var maxLength = Math.Min(inputLengths.Max(), MaxTokens);
//
//                 var batchSize = sentences.Count;
//                 var inputIdsTensor = new DenseTensor<long>(new ReadOnlySpan<int>([batchSize, maxLength]));
//                 var attentionMaskTensor = new DenseTensor<long>(new ReadOnlySpan<int>([batchSize, maxLength]));
//
//                 var inputIdsSpan = inputIdsTensor.Buffer.Span;
//                 var attentionMaskSpan = attentionMaskTensor.Buffer.Span;
//                 for (var i = 0; i < batchSize; i++)
//                 {
//                     var inputId = inputIds[i];
//                     var offset0 = i * maxLength;
//                     for (var j = 0; j < maxLength; j++)
//                     {
//                         var offset = offset0 + j;
//                         if (j < inputId.Length)
//                         {
//                             inputIdsSpan[offset] = inputId[j];
//                             attentionMaskSpan[offset] = 1; // Attention mask for valid tokens
//                         }
//                         else
//                         {
//                             inputIdsSpan[offset] = 0; // Padding
//                             attentionMaskSpan[offset] = 0; // Padding
//                         }
//                     }
//                 }
//
//                 var tokenTypeIdsTensor = new DenseTensor<long>(new ReadOnlySpan<int>([batchSize, maxLength]));
//                 tokenTypeIdsTensor.Fill(0);
//
//                 using var outputs = session.Run(
//                 [
//                     NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
//                     NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
//                     NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor),
//                 ]);
//
//                 var lastHidden = outputs[0].AsTensor<float>().ToDenseTensor(); // shape: [batch, seq, hidden]
//                 var lastHiddenSpan = lastHidden.Buffer.Span;
//                 var hiddenSize = lastHidden.Dimensions[2];
//                 var embeddings = new Embedding[batchSize];
//
//                 for (var i = 0; i < batchSize; i++)
//                 {
//                     var embedding = new float[hiddenSize];
//
//                     float sumMask = 0;
//                     for (var j = 0; j < maxLength; j++)
//                     {
//                         var offset = i * maxLength + j;
//                         if (attentionMaskSpan[offset] <= 0) continue;
//                         sumMask += 1;
//
//                         for (var h = 0; h < hiddenSize; h++) embedding[h] += lastHiddenSpan[offset * hiddenSize + h];
//                     }
//
//                     for (var h = 0; h < hiddenSize; h++) embedding[h] /= sumMask;
//
//                     var norm = 0f;
//                     for (var h = 0; h < hiddenSize; h++) norm += embedding[h] * embedding[h];
//                     norm = MathF.Sqrt(norm);
//                     for (var h = 0; h < hiddenSize; h++) embedding[h] /= norm;
//
//                     embeddings[i] = new Embedding(embedding);
//                 }
//
//                 return embeddings;
//             },
//             cancellationToken);
//     }
// }