using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Amaigoma
{
   using DataTransformer = Func<IEnumerable<double>, double>;

   // TODO Rename class to something else than "Transformer"
   public record PassThroughTransformer : IEnumerable<DataTransformer> // ncrunch: no coverage
   {
      public int DataCount { get; private set; }

      public PassThroughTransformer(int dataCount)
      {
         DataCount = dataCount;
      }

      public IEnumerator<DataTransformer> GetEnumerator()
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

      IEnumerator IEnumerable.GetEnumerator()
      { // ncrunch: no coverage
         return this.GetEnumerator(); // ncrunch: no coverage
      } // ncrunch: no coverage
   }
}