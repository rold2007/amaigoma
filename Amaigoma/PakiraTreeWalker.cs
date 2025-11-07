namespace Amaigoma
{
   public sealed record PakiraTreeWalker // ncrunch: no coverage
   {
      private PakiraTree Tree { get; }
      private TanukiETL TanukiETL { get; }

      public PakiraTreeWalker(PakiraTree tree, TanukiETL tanukiETL)
      {
         Tree = tree;
         TanukiETL = tanukiETL;
      }

      public BinaryTreeLeaf PredictLeaf(int id)
      {
         int nextNodeIndex = 0;
         BinaryTreeNode? node = Tree.Node(nextNodeIndex);

         while (node.HasValue)
         {
            double columnValue = TanukiETL.TanukiDataTransformer(id, node.Value.featureIndex);

            nextNodeIndex = (columnValue <= node.Value.splitThreshold) ? node.Value.leftNodeIndex : node.Value.rightNodeIndex;
            node = Tree.Node(nextNodeIndex);
         }

         BinaryTreeLeaf leaf = Tree.Leaf(nextNodeIndex);

         return new(leaf.id, leaf.labelValue);
      }
   }
}
