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

         pakiraTree.Node(0).HasValue.ShouldBeFalse();
      }

      // UNDONE Restore unit tests and make sure to have 100% code coverage for PakiraTree from these tests
      //[Fact]
      //public void AddNodeTopBottom()
      //{
      //   BinaryTreeNode rootNode = new(6, 7);
      //   BinaryTreeLeaf leftLeaf = new(8);
      //   BinaryTreeLeaf rightLeaf = new(9);
      //   PakiraTree pakiraTree = new PakiraTree().AddNode(rootNode, leftLeaf, rightLeaf);

      //   BinaryTreeNode subNode = new(10, 11);

      //   BinaryTreeLeaf newLeftLeaf = new(12);
      //   BinaryTreeLeaf newRightLeaf = new(13);

      //   pakiraTree = pakiraTree.ReplaceLeaf(rootNode, rightLeaf, new PakiraTree().AddNode(subNode, newLeftLeaf, newRightLeaf));

      //   subNode = new BinaryTreeNode(14, 15);
      //   newLeftLeaf = new BinaryTreeLeaf(16);
      //   newRightLeaf = new BinaryTreeLeaf(17);

      //   pakiraTree = pakiraTree.ReplaceLeaf(rootNode, leftLeaf, new PakiraTree().AddNode(subNode, newLeftLeaf, newRightLeaf));

      //   pakiraTree.Root.ShouldBe(rootNode);
      //   pakiraTree.GetNodes().Count().ShouldBe(3);
      //}

      //[Fact]
      //public void BinaryTreeNodeComparer()
      //{
      //   BinaryTreeNode rootNode = new(6, 7);
      //   BinaryTreeLeaf leftLeaf = new(8);
      //   BinaryTreeLeaf rightLeaf = new(9);
      //   PakiraTree pakiraTree = new PakiraTree().AddNode(rootNode, leftLeaf, rightLeaf);

      //   pakiraTree.ReplaceLeaf(rootNode, leftLeaf, new PakiraTree().AddNode(new BinaryTreeNode(6, 7), new BinaryTreeLeaf(8), new BinaryTreeLeaf(9)));
      //}
   }
}
