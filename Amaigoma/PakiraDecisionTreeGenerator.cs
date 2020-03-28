namespace Amaigoma
{
   using System;

   /// <summary>A decision tree generator.</summary>
   public class PakiraDecisionTreeGenerator
   {
      /// <summary>Gets or sets the width.</summary>
      /// <value>The width.</value>
      public int Width { get; set; }

      /// <summary>Constructor.</summary>
      /// <param name="descriptor">the descriptor.</param>
      public PakiraDecisionTreeGenerator()
      {
         Width = 2;
      }
      /// <summary>Constructor.</summary>
      /// <exception cref="InvalidOperationException">Thrown when the requested operation is invalid.</exception>
      /// <param name="depth">(Optional) The depth.</param>
      /// <param name="width">(Optional) the width.</param>
      /// <param name="descriptor">(Optional) the descriptor.</param>
      /// <param name="hint">(Optional) the hint.</param>
      public PakiraDecisionTreeGenerator(
          int width = 2)
      {
         if (width < 2)
            throw new InvalidOperationException("Cannot set dt tree width to less than 2!");

         Width = width;
      }

      private PakiraNode BuildLeafNode(double val)
      {
         return new PakiraNode()
         {
            IsLeaf = true,
            Value = val
         };
      }
   }
}
