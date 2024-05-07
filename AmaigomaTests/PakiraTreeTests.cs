using Amaigoma;
using Shouldly;
using System.Linq;
using Xunit;

namespace AmaigomaTests
{
   public record PakiraTreeTests // ncrunch: no coverage
   {
      [Fact]
      public void Constructor()
      {
         PakiraTree pakiraTree = new();

         pakiraTree.Root.ShouldBeNull();
      }

      [Fact]
      public void AddNodeTopBottom()
      {
         PakiraNode rootNode = new(6, 7);
         PakiraLeaf leftLeaf = new(8);
         PakiraLeaf rightLeaf = new(9);
         PakiraTree pakiraTree = new PakiraTree().AddNode(rootNode, leftLeaf, rightLeaf);

         PakiraNode subNode = new(10, 11);

         PakiraLeaf newLeftLeaf = new(12);
         PakiraLeaf newRightLeaf = new(13);

         pakiraTree = pakiraTree.ReplaceLeaf(rootNode, rightLeaf, new PakiraTree().AddNode(subNode, newLeftLeaf, newRightLeaf));

         subNode = new PakiraNode(14, 15);
         newLeftLeaf = new PakiraLeaf(16);
         newRightLeaf = new PakiraLeaf(17);

         pakiraTree = pakiraTree.ReplaceLeaf(rootNode, leftLeaf, new PakiraTree().AddNode(subNode, newLeftLeaf, newRightLeaf));

         pakiraTree.Root.ShouldBe(rootNode);
         pakiraTree.GetNodes().Count().ShouldBe(3);
      }

      [Fact]
      public void PakiraNodeComparer()
      {
         PakiraNode rootNode = new(6, 7);
         PakiraLeaf leftLeaf = new(8);
         PakiraLeaf rightLeaf = new(9);
         PakiraTree pakiraTree = new PakiraTree().AddNode(rootNode, leftLeaf, rightLeaf);

         pakiraTree.ReplaceLeaf(rootNode, leftLeaf, new PakiraTree().AddNode(new PakiraNode(6, 7), new PakiraLeaf(8), new PakiraLeaf(9)));
      }
   }
}
