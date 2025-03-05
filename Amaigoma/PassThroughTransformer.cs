using System;
using System.Collections.Generic;
using System.Linq;

namespace Amaigoma
{
   using DataTransformer = Func<IEnumerable<double>, double>;

   // TODO Rename class to something else than "Transformer"
   public record PassThroughTransformer
   {
      public int DataCount { get; private set; }

      public PassThroughTransformer(int dataCount)
      {
         DataCount = dataCount;
      }

      // public IEnumerable<DataTransformerIndices> DataTransformersIndices
      // {
      //    get
      //    {
      //       for (int i = 0; i < DataCount; i++)
      //       {
      //          int j = i;

      //          yield return (featureIndex) =>
      //          {
      //             return [ j ];
      //          };
      //       }
      //    }
      // }

      public IEnumerable<DataTransformer> DataTransformers
      {
         get
         {
            for (int i = 0; i < DataCount; i++)
            {
               int j = i;

               yield return (list) =>
               {
                  return list.ElementAt(j);
               };
            }
         }
      }
   }
}