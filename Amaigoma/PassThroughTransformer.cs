namespace Amaigoma
{
   using System.Collections.Generic;

   public class PassThroughTransformer
   {
      public PassThroughTransformer()
      {
      }

      public IList<double> ConvertAll(IList<double> list)
      {
         return list;
      }
   }
}