using System;
using System.Linq;

namespace mccsx.Statistics
{
    /// <summary>
    /// https://en.wikipedia.org/wiki/Cosine_similarity
    /// </summary>
    public class CosineSimilarityMeasure : IVectorDistanceMeasure
    {
        public string Name => "Cosine";

        public double Measure<TKey>(IVector<TKey> vec1, IVector<TKey> vec2)
            where TKey : notnull
        {
            var keys = vec1.UnionKeys(vec2).Where(key => !double.IsNaN(vec1[key]) && !double.IsNaN(vec2[key])).ToArray();

            if (keys.Length == 0)
                return double.NaN;

            // norm of two vectors
            double normLeft = Math.Sqrt(keys.Sum(key => Math.Pow(vec1[key], 2)));
            double normRight = Math.Sqrt(keys.Sum(key => Math.Pow(vec2[key], 2)));

            if (normLeft == 0.0 || normRight == 0.0)
                return double.NaN;

            // dot product over two norms
            return keys.Sum(key => vec1[key] * vec2[key]) / normLeft / normRight;
        }
    }
}
