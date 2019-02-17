namespace Amaigoma
{
   using System;
   using numl.Math.Information;
   using numl.Math.LinearAlgebra;

   public class DispersionError : Impurity
   {
      public override double Calculate(Vector x)
      {
         if (x == null)
            throw new InvalidOperationException("x does not exist!");

         return 0.0;
      }
   }
}
