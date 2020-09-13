using System;
using System.Linq;

namespace mccsx.Statistics
{
    /// <summary>
    /// Pearson Correlation Coefficient
    /// https://en.wikipedia.org/wiki/Pearson_correlation_coefficient
    /// </summary>
    public class CorrelationCoefficientMeasure : IVectorDistanceMeasure
    {
        public string Name => "Correlation";

        public double Measure<TKey>(IVector<TKey> vec1, IVector<TKey> vec2)
            where TKey : notnull
        {
            var keys = vec1.UnionKeys(vec2).Where(key => !double.IsNaN(vec1[key]) && !double.IsNaN(vec2[key])).ToArray();

            if (keys.Length == 0)
                return double.NaN;

            // mean value
            double averageLeft = keys.Sum(key => vec1[key]) / keys.Length;
            double averageRight = keys.Sum(key => vec2[key]) / keys.Length;

            // population standard deviation
            double sdevLeft = Math.Sqrt(keys.Sum(key => Math.Pow(vec1[key] - averageLeft, 2)) / keys.Length);
            double sdevRight = Math.Sqrt(keys.Sum(key => Math.Pow(vec2[key] - averageRight, 2)) / keys.Length);

            if (sdevLeft == 0.0 || sdevRight == 0.0)
                return double.NaN;

            // population covariance
            double r = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                for (int j = i + 1; j < keys.Length; j++)
                {
                    r += (vec1[keys[i]] - vec1[keys[j]]) * (vec2[keys[i]] - vec2[keys[j]]);
                }
            }

            return r / keys.Length / keys.Length / sdevLeft / sdevRight;
        }
    }
}
