namespace Amaigoma
{
   using System;
   using System.Linq;
   using System.Collections.Generic;

   /// <summary>
   /// Class Tree.
   /// </summary>
   /// <seealso cref="numl.Data.Graph" />
   public class PakiraTree : PakiraGraph
   {
      /// <summary>
      /// Gets or sets the root.
      /// </summary>
      /// <value>The root.</value>
      public PakiraNode Root { get; set; }

      /// <summary>
      /// Returns the hash code for this Tree.
      /// </summary>
      /// <returns></returns>
      public override int GetHashCode()
      {
         return base.GetHashCode();
      }

      /// <summary>
      /// Determines whether the specified object is equal to the current root Tree object.
      /// </summary>
      /// <param name="obj">Object to test.</param>
      /// <returns></returns>
      public override bool Equals(object obj)
      {
         if (obj is PakiraTree)
         {
            if (!base.Equals((PakiraGraph)obj))
               return false;
            else if (!((PakiraTree)obj).Root.Equals(Root))
               return false;
            else
               return true;
         }
         else
            return false;
      }
   }
}
