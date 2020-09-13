﻿using mccsx.Statistics;
using System;
using System.Diagnostics;

namespace mccsx
{
    internal static class MesaureExtensions
    {
        public static IVectorDistanceMeasure SimilarityMeasure(this Measure measure)
        {
            var simClassType = EnumAnnotationHelper<Measure>.GetAttribute<MeasureImplAttribute>(measure).SimilarityClass;
            var obj = Activator.CreateInstance(simClassType);
            Debug.Assert(obj != null && obj is IVectorDistanceMeasure);
            return (obj as IVectorDistanceMeasure)!;
        }

        public static IVectorDistanceMeasure DistanceMeasure(this Measure measure)
        {
            var simClassType = EnumAnnotationHelper<Measure>.GetAttribute<MeasureImplAttribute>(measure).SimilarityClass;
            var obj = Activator.CreateInstance(simClassType);
            Debug.Assert(obj != null && obj is IVectorDistanceMeasure);
            return (obj as IVectorDistanceMeasure)!;
        }
    }
}
