namespace Amaigoma
{
   using numl.Model;
   using numl.Utils;
   using System;
   using System.Collections.Generic;

   public class CustomFeature : Property
   {
      public CustomFeature()
      {
      }

      /// <summary>
      /// Used as a preprocessing step when overridden. Can be used to look at the entire data set as a
      /// whole before converting single elements.
      /// </summary>
      /// <param name="examples">Examples.</param>
      public override void PreProcess(IEnumerable<object> examples)
      {
         return;
      }

      /// <summary>
      /// Used as a preprocessing step when overriden. Can be used to look at the current object in
      /// question before converting single elements.
      /// </summary>
      /// <param name="example">Example.</param>
      public override void PreProcess(object example)
      {
         return;
      }

      /// <summary>
      /// Used as a postprocessing step when overridden. Can be used to look at the entire data set as
      /// a whole after converting single elements.
      /// </summary>
      /// <param name="examples">Examples.</param>
      public override void PostProcess(IEnumerable<object> examples)
      {
         return;
      }

      /// <summary>
      /// Used as a postprocessing step when overriden. Can be used to look at the current object in
      /// question fater converting single elements.
      /// </summary>
      /// <param name="example">.</param>
      public override void PostProcess(object example)
      {

      }

      /// <summary>Convert the numeric representation back to the original type.</summary>
      /// <param name="val">.</param>
      /// <returns>An object.</returns>
      public override object Convert(double val)
      {
         return Ject.Convert(val, this.Type);
      }

      /// <summary>Convert an object to a list of numbers.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="o">Object.</param>
      /// <returns>Lazy list of doubles.</returns>
      public override IEnumerable<double> Convert(object o)
      {
         throw new NotImplementedException();
      }

      /// <summary>
      /// Retrieve the list of expanded columns. If there is a one-to-one correspondence between the
      /// type and its expansion it will return a single value/.
      /// </summary>
      /// <returns>
      /// An enumerator that allows foreach to be used to process the columns in this collection.
      /// </returns>
      public override IEnumerable<string> GetColumns()
      {
         yield return this.Name;
      }
   }

   public class SumFeature : CustomFeature
   {
      private readonly int firstIndex;
      private readonly int secondIndex;

      public SumFeature(int firstIndex, int secondIndex) : base()
      {
         this.firstIndex = firstIndex;
         this.secondIndex = secondIndex;
      }

      /// <summary>Convert an object to a list of numbers.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="o">Object.</param>
      /// <returns>Lazy list of doubles.</returns>
      public override IEnumerable<double> Convert(object o)
      {
         double[] data = o as double[];

         yield return (data[firstIndex] + data[secondIndex]);
      }
   }

   public class ProductFeature : CustomFeature
   {
      private readonly int firstIndex;
      private readonly int secondIndex;

      public ProductFeature(int firstIndex, int secondIndex) : base()
      {
         this.firstIndex = firstIndex;
         this.secondIndex = secondIndex;
      }

      /// <summary>Convert an object to a list of numbers.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="o">Object.</param>
      /// <returns>Lazy list of doubles.</returns>
      public override IEnumerable<double> Convert(object o)
      {
         double[] data = o as double[];

         yield return (data[firstIndex] * data[secondIndex]);
      }
   }

   public class RandomFeature : CustomFeature
   {
      public RandomFeature() : base()
      {
      }

      /// <summary>Convert an object to a list of numbers.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="o">Object.</param>
      /// <returns>Lazy list of doubles.</returns>
      public override IEnumerable<double> Convert(object o)
      {
         yield return (numl.Math.Probability.Sampling.GetNormal(0, 10));
      }
   }
}
