﻿using Amaigoma;
using Shouldly;
using System.Linq;
using Xunit;

namespace AmaigomaTests
{
   public class PakiraTreeTests
   {
      [Fact]
      public void Constructor()
      {
         PakiraTree pakiraTree = PakiraTree.Empty;

         pakiraTree.Root.ShouldBeNull();
      }

      [Fact]
      public void AddNodeTopBottom()
      {
         PakiraNode rootNode = new PakiraNode(6, 7);
         PakiraLeaf leftLeaf = new PakiraLeaf(8);
         PakiraLeaf rightLeaf = new PakiraLeaf(9);
         PakiraTree pakiraTree = PakiraTree.Empty.AddNode(rootNode, leftLeaf, rightLeaf);

         PakiraNode subNode = new PakiraNode(10, 11);

         PakiraLeaf newLeftLeaf = new PakiraLeaf(12);
         PakiraLeaf newRightLeaf = new PakiraLeaf(13);

         pakiraTree = pakiraTree.ReplaceLeaf(rightLeaf, PakiraTree.Empty.AddNode(subNode, newLeftLeaf, newRightLeaf));

         subNode = new PakiraNode(14, 15);
         newLeftLeaf = new PakiraLeaf(16);
         newRightLeaf = new PakiraLeaf(17);

         pakiraTree = pakiraTree.ReplaceLeaf(leftLeaf, PakiraTree.Empty.AddNode(subNode, newLeftLeaf, newRightLeaf));

         pakiraTree.Root.ShouldBe(rootNode);
         pakiraTree.GetNodes().Count().ShouldBe(7);
      }
   }
}
