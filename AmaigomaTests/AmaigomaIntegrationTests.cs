using Amaigoma;
using NCrunch.Framework;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

// UNDONE Bring back code coverage to 100%

// TODO January 15th 2024: New algorithm idea. The strength of each node can be validated if, and only if, there are enough leaves under it to apply
// the logic of swapping the node condition and validating the success rate on train data. For nodes which do not have enough leaves under, this process
// will probably not give reliable results. The solution is probably to prune these nodes. This will force some leaves to have more than one class. So
// more trees need to be created, this way each data may eventually fall in a leaf with a single class. Not sure how to determine how many trees are needed
// to prevent having data to always fall in a multi-class leaf. Maybe a priority list of trees can be created, and each time a tree returns a multiclass
// it should lower its priority. This will not prevent infinite multiclass leaves, but it may help select the tree which returns a multiclass leaf less often.
namespace AmaigomaTests
{
   using DataTransformer = Converter<IEnumerable<double>, IEnumerable<double>>;

   public record IntegrationTestDataSet // ncrunch: no coverage
   {
      public string filename;
      public ImmutableList<Rectangle> regions;
      public ImmutableList<int> labels;

      public IntegrationTestDataSet(string filename, ImmutableList<Rectangle> regions, ImmutableList<int> labels)
      {
         regions.Count.ShouldBe(labels.Count);

         this.filename = filename;
         this.regions = regions;
         this.labels = labels;
      }
   }

   public record AverageWindowFeature // ncrunch: no coverage
   {
      private ImmutableDictionary<int, SampleData> Samples;
      private Buffer2D<ulong> IntegralImage;
      private int FeatureWindowSize;
      private int HalfFeatureWindowSize;

      public AverageWindowFeature(ImmutableDictionary<int, SampleData> positions, Buffer2D<ulong> integralImage, int featureWindowSize)
      {
         Samples = positions;
         IntegralImage = integralImage;
         FeatureWindowSize = featureWindowSize;
         HalfFeatureWindowSize = featureWindowSize / 2;
      }

      public IEnumerable<double> ConvertAll(int id)
      {
         Point position = Samples[id].Position;
         List<double> newSample = new((FeatureWindowSize + 1) * (FeatureWindowSize + 1));

         int top = position.Y + HalfFeatureWindowSize;
         int xPosition = position.X + HalfFeatureWindowSize;

         xPosition.ShouldBePositive();

         // UNDONE I should get rid of the data extractors. Most of the time the data transformers don't need the full data sample, except in train mode,
         // so it is slow for nothing. The data transformer could fetch only what it needs and back it up with a SabotenCache.
         // UNDONE Try to apply this solution to see if it is faster, although it will probably allocate more: https://github.com/SixLabors/ImageSharp/discussions/1666#discussioncomment-876494
         // +1 length to support first row of integral image
         for (int y2 = -HalfFeatureWindowSize; y2 <= HalfFeatureWindowSize + 1; y2++)
         {
            int yPosition = top + y2;

            yPosition.ShouldBeGreaterThanOrEqualTo(0);

            // +1 length to support first column of integral image
            foreach (ulong integralValue in IntegralImage.DangerousGetRowSpan(yPosition).Slice(xPosition - HalfFeatureWindowSize, FeatureWindowSize + 1))
            {
               newSample.Add(integralValue);
            }
         }

         return newSample;
      }

      public int ExtractLabel(int id)
      {
         return Samples[id].Label;
      }
   }

   public struct DataSet
   {
      public List<IntegrationTestDataSet> train = new List<IntegrationTestDataSet>();
      public List<IntegrationTestDataSet> validation = new List<IntegrationTestDataSet>();
      public List<IntegrationTestDataSet> test = new List<IntegrationTestDataSet>();

      public DataSet()
      {
      }
   }

   public struct AccuracyResult
   {
      public ImmutableHashSet<PakiraLeaf> leavesBefore;
      public ImmutableHashSet<PakiraLeaf> leavesAfter;
   }

   public struct SampleData
   {
      public Point Position;
      public int Label;
   }

   // TODO The integration test could output interesting positions to be validated and added to the test
   public record AmaigomaIntegrationTests // ncrunch: no coverage
   {
      static readonly int uppercaseA = 1; // ncrunch: no coverage
      static readonly int other = 2; // ncrunch: no coverage

      // TODO Removed some samples in Train, Validation and Test sets to be able to run faster until the performances are improved
      static private readonly ImmutableList<Rectangle> trainNotUppercaseA_507484246_Rectangles = ImmutableList<Rectangle>.Empty.AddRange(new Rectangle[] // ncrunch: no coverage
       {
          new Rectangle(83, 150, 1, 1),
          new Rectangle(0, 0, 300, 100),
          new Rectangle(624, 140, 1, 1),
          new Rectangle(670, 140, 1, 1),
          new Rectangle(688, 140, 1, 1),
          new Rectangle(36, 196, 1, 1),
          new Rectangle(192, 197, 1, 1),
          new Rectangle(181, 213, 1, 1),
          new Rectangle(576, 216, 1, 1),
          new Rectangle(603, 217, 1, 1),
          new Rectangle(658, 217, 1, 1),
          new Rectangle(109, 333, 1, 1),
          new Rectangle(127, 333, 1, 1),
          //new Rectangle(520, 40, 230, 90),
          //new Rectangle(20, 420, 380, 80),
      });

      static private readonly ImmutableList<int> trainNotUppercaseA_507484246 = ImmutableList<int>.Empty.AddRange(new int[] // ncrunch: no coverage
       {
          uppercaseA,
          other,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          uppercaseA,
          //other,
          //other,
      });

      static private readonly ImmutableList<Rectangle> validationNotUppercaseA_507484246_Rectangles = ImmutableList<Rectangle>.Empty.AddRange(new Rectangle[] // ncrunch: no coverage
       {
         new Rectangle(190, 540, 280, 20),
         //new Rectangle(20, 555, 480, 215),
      });

      static private readonly ImmutableList<int> validationNotUppercaseA_507484246 = ImmutableList<int>.Empty.AddRange(new int[] // ncrunch: no coverage
       {
         other,
         //other,
      });

      static private readonly ImmutableList<Rectangle> testNotUppercaseA_507484246_Rectangles = ImmutableList<Rectangle>.Empty.AddRange(new Rectangle[] // ncrunch: no coverage
       {
         new Rectangle(228, 334, 1, 1),
         new Rectangle(283, 335, 1, 1),
         new Rectangle(153, 408, 1, 1),
         new Rectangle(217, 519, 1, 1),
         new Rectangle(155, 549, 1, 1),
         new Rectangle(218, 790, 1, 1),
         new Rectangle(411, 836, 1, 1),
         new Rectangle(137, 851, 1, 1),
         new Rectangle(257, 851, 1, 1),
         new Rectangle(605, 852, 1, 1),
         //new Rectangle(520, 550, 250, 216),
         new Rectangle(95, 810, 500, 20),
         //new Rectangle(20, 900, 756, 70),
         //new Rectangle(180, 960, 310, 35)
      });

      static private readonly ImmutableList<int> testNotUppercaseA_507484246 = ImmutableList<int>.Empty.AddRange(new int[] // ncrunch: no coverage
       {
         uppercaseA,
         uppercaseA,
         uppercaseA,
         uppercaseA,
         uppercaseA,
         uppercaseA,
         uppercaseA,
         uppercaseA,
         uppercaseA,
         uppercaseA,
         //other,
         other,
         //other,
         //other,
      });

      public static IEnumerable<object[]> GetUppercaseA_507484246_Data()
      {
         DataSet dataSet = new DataSet();
         IntegrationTestDataSet trainIntegrationTestDataSet = new IntegrationTestDataSet(@"assets/text-extraction-for-ocr/507484246.tif", trainNotUppercaseA_507484246_Rectangles, trainNotUppercaseA_507484246);
         IntegrationTestDataSet validationIntegrationTestDataSet = new IntegrationTestDataSet(@"assets/text-extraction-for-ocr/507484246.tif", validationNotUppercaseA_507484246_Rectangles, validationNotUppercaseA_507484246);
         IntegrationTestDataSet testIntegrationTestDataSet = new IntegrationTestDataSet(@"assets/text-extraction-for-ocr/507484246.tif", testNotUppercaseA_507484246_Rectangles, testNotUppercaseA_507484246);

         dataSet.train.Add(trainIntegrationTestDataSet);
         dataSet.validation.Add(validationIntegrationTestDataSet);
         dataSet.test.Add(testIntegrationTestDataSet);

         yield return new object[] { dataSet };
      }

      private readonly ITestOutputHelper output;

      public AmaigomaIntegrationTests(ITestOutputHelper output)
      {
         this.output = output;
      }

      ImmutableDictionary<int, SampleData> LoadDataSamples(ImmutableList<Rectangle> rectangles, ImmutableList<int> labels, int startingIndex)
      {
         ImmutableDictionary<int, SampleData> result = ImmutableDictionary<int, SampleData>.Empty;
         // TODO Move the rectangles and labels in a dictionary to get both values at the same time in the foreach
         int labelsIndex = 0;

         foreach (Rectangle rectangle in rectangles)
         {
            int sampleClass = labels[labelsIndex];

            for (int y = rectangle.Top; y < rectangle.Bottom; y++)
            {
               for (int x = rectangle.Left; x < rectangle.Right; x++)
               {
                  result = result.Add(startingIndex, new SampleData { Position = new Point(x, y), Label = sampleClass });
                  startingIndex++;
               }
            }

            labelsIndex++;
         }

         return result;
      }

      AccuracyResult ComputeAccuracy(PakiraDecisionTreeModel pakiraDecisionTreeModel, IEnumerable<int> ids, TanukiTransformers tanukiTransformers)
      {
         ImmutableHashSet<PakiraLeaf> leaves = ImmutableHashSet<PakiraLeaf>.Empty.Union(pakiraDecisionTreeModel.Tree.GetLeaves().Select(x => x.Value));
         AccuracyResult accuracyResult = new AccuracyResult();

         accuracyResult.leavesBefore = leaves;

         // TODO Move the rectangles and labels in a dictionary to get both values at the same time in the foreach
         PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, tanukiTransformers);

         foreach (int id in ids)
         {
            PakiraLeaf pakiraLeafResult = pakiraTreeWalker.PredictLeaf(id);
            int sampleClass = tanukiTransformers.TanukiLabelExtractor(id);

            if (pakiraLeafResult.LabelValues.Count() > 1 || !pakiraLeafResult.LabelValues.Contains(sampleClass))
            {
               leaves = leaves.Remove(pakiraLeafResult);

               if (leaves.Count == 0)
               {
                  break;
               }
            }
         }

         accuracyResult.leavesAfter = leaves;

         return accuracyResult;
      }

      [Theory]
      [MemberData(nameof(GetUppercaseA_507484246_Data))]
      [Timeout(600000)]
      public void UppercaseA_507484246(DataSet dataSet)
      {
         const int FeatureFullWindowSize = 17;
         string imagePath = dataSet.train[0].filename;
         ImmutableList<Rectangle> trainRectangles = dataSet.train[0].regions;
         ImmutableList<int> trainLabels = dataSet.train[0].labels;
         ImmutableList<Rectangle> validationRectangles = dataSet.validation[0].regions;
         ImmutableList<int> validationLabels = dataSet.validation[0].labels;
         ImmutableList<Rectangle> testRectangles = dataSet.test[0].regions;
         ImmutableList<int> testLabels = dataSet.test[0].labels;

         trainRectangles.Count.ShouldBe(trainLabels.Count);
         validationRectangles.Count.ShouldBe(validationLabels.Count);
         testRectangles.Count.ShouldBe(testLabels.Count);

         const int halfFeatureWindowSize = FeatureFullWindowSize / 2;
         string fullImagePath = Path.Combine(Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(Assembly.GetExecutingAssembly().Location).AbsolutePath)), imagePath);

         PakiraDecisionTreeGenerator pakiraGenerator = new();
         Image<L8> imageWithOverscan;

         {
            Image<L8> fullTextImage = Image.Load<L8>(fullImagePath);

            // TODO Need to support different background values
            imageWithOverscan = new Image<L8>(fullTextImage.Width + (2 * halfFeatureWindowSize) + 1, fullTextImage.Height + (2 * halfFeatureWindowSize) + 1, new L8(255));

            // TODO Move this to a globally available helper method
            // There is a +1 in coordinates to make sure we have at least one row and one column that we never use at the beginning so that we never go outside the image for integral values
            imageWithOverscan.Mutate(x => x.DrawImage(fullTextImage, new Point(halfFeatureWindowSize + 1, halfFeatureWindowSize + 1), 1.0f));
         }

         Buffer2D<ulong> integralImage = imageWithOverscan.CalculateIntegralImage();
         ImmutableDictionary<int, SampleData> trainPositions;
         ImmutableDictionary<int, SampleData> validationPositions;
         ImmutableDictionary<int, SampleData> testPositions;

         trainPositions = LoadDataSamples(trainRectangles, trainLabels, 0);
         validationPositions = LoadDataSamples(validationRectangles, validationLabels, trainPositions.Count());
         testPositions = LoadDataSamples(testRectangles, testLabels, trainPositions.Count() + validationPositions.Count());

         // TODO All data transformers should have the same probability of being chosen, otherwise the AverageTransformer with a bigger windowSize will barely be selected
         DataTransformer dataTransformers = null;

         // TODO Removed a data transformer to be able to run faster until the performances are improved
         dataTransformers += new AverageTransformer(17, FeatureFullWindowSize).ConvertAll;
         dataTransformers += new AverageTransformer(7, FeatureFullWindowSize).ConvertAll;
         dataTransformers += new AverageTransformer(5, FeatureFullWindowSize).ConvertAll;
         dataTransformers += new AverageTransformer(3, FeatureFullWindowSize).ConvertAll;
         // dataTransformers += new AverageTransformer(1).ConvertAll;

         AverageWindowFeature trainDataExtractor = new AverageWindowFeature(trainPositions, integralImage, FeatureFullWindowSize);
         AverageWindowFeature validationDataExtractor = new AverageWindowFeature(validationPositions, integralImage, FeatureFullWindowSize);
         AverageWindowFeature testDataExtractor = new AverageWindowFeature(testPositions, integralImage, FeatureFullWindowSize);
         TanukiTransformers trainTanukiTransformers = new(trainPositions.Keys.First(), trainDataExtractor.ConvertAll, dataTransformers, trainDataExtractor.ExtractLabel);
         TanukiTransformers validationTanukiTransformers = new(validationPositions.Keys.First(), validationDataExtractor.ConvertAll, dataTransformers, validationDataExtractor.ExtractLabel);
         TanukiTransformers testTanukiTransformers = new(testPositions.Keys.First(), testDataExtractor.ConvertAll, dataTransformers, testDataExtractor.ExtractLabel);

         PakiraDecisionTreeModel pakiraDecisionTreeModel = new();

         pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, new[] { trainPositions.Keys.First() }, trainTanukiTransformers);

         // TODO Evaluate the possibility of using shallow trees to serve as sub-routines. The features could be chosen based on the
         // best discrimination, like it was done a while ago. This will result in categories instead of a scalar so the leaves will need to be recombined
         // to provide a binary (scalar) answer. Many strategies could be use to combine leaves. All the left ones vs right ones, random?
         int previousRegenerateTreeCount = -1;
         int previousRegenerateTreeCountBatch = -1;
         int regenerateTreeCount = 0;
         bool processBackgroundTrainData = true;
         int batchSize = 0;
         int totalTrainSamples = trainPositions.Keys.Count();

         AccuracyResult validationAccuracyResult;
         AccuracyResult testAccuracyResult;
         AccuracyResult trainAccuracyResult;

         while (processBackgroundTrainData)
         {
            previousRegenerateTreeCount = regenerateTreeCount;
            processBackgroundTrainData = false;

            // TODO Run many tree generations to evaluate if the average accuracy is lower when the root is using a data transformer
            // with a lower size (1 vs 15)
            // TODO Move the batch processing/training along with the tree evaluation (true/false positive leaves) in an utility class outside of the Test classes, inside the main library
            // TODO Note this methodology somewhere: When the validation set contains too many unevaluated leaves we need to apply one of the following solution:
            // - Increase validation set size
            // - Optimize the tree size by replacing nodes with better discriminating nodes, thus reducing the number of leaves and/or the depth of the tree
            // - Other?
            for (int i = 0; i < totalTrainSamples; i += batchSize)
            {
               batchSize = Math.Min(100, Math.Max(20, pakiraDecisionTreeModel.Tree.GetLeaves().Count()));
               IEnumerable<int> batchSamples = trainPositions.Keys.Skip(i).Take(batchSize);

               bool processBatch = true;
               PakiraTreeWalker pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, trainTanukiTransformers);

               // TODO The validation set should be used to identify the leaves which are not predicting correctly. Then find
               //       some data in the train set to improve these leaves
               while (processBatch)
               {
                  int batchIndex = 0;

                  previousRegenerateTreeCountBatch = regenerateTreeCount;

                  foreach (int id in batchSamples)
                  {
                     int expectedLabel = trainPositions[id].Label;
                     IEnumerable<int> resultLabels = pakiraTreeWalker.PredictLeaf(id).LabelValues;

                     if (resultLabels.Count() > 1 || !resultLabels.Contains(expectedLabel))
                     {
                        pakiraDecisionTreeModel = pakiraGenerator.Generate(pakiraDecisionTreeModel, new[] { id }, trainTanukiTransformers);
                        pakiraTreeWalker = new PakiraTreeWalker(pakiraDecisionTreeModel.Tree, trainTanukiTransformers);

                        IEnumerable<int> labelValues = pakiraTreeWalker.PredictLeaf(id).LabelValues;

                        labelValues.Count().ShouldBe(1);
                        labelValues.First().ShouldBe(expectedLabel);

                        regenerateTreeCount++;
                     }

                     batchIndex++;
                  }

                  processBatch = (previousRegenerateTreeCountBatch != regenerateTreeCount);
               }
            }

            trainAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, trainPositions.Keys, trainTanukiTransformers);
            validationAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, validationPositions.Keys, validationTanukiTransformers);
            testAccuracyResult = ComputeAccuracy(pakiraDecisionTreeModel, testPositions.Keys, testTanukiTransformers);

            // TODO Improve test output
            output.WriteLine(trainAccuracyResult.leavesAfter.Count().ToString());
            output.WriteLine(trainAccuracyResult.leavesBefore.Count().ToString());
            output.WriteLine(validationAccuracyResult.leavesAfter.Count().ToString());
            output.WriteLine(validationAccuracyResult.leavesBefore.Count().ToString());
            output.WriteLine(testAccuracyResult.leavesAfter.Count().ToString());
            output.WriteLine(testAccuracyResult.leavesBefore.Count().ToString());
            output.WriteLine("---");

            processBackgroundTrainData = (trainAccuracyResult.leavesBefore.Count != trainAccuracyResult.leavesAfter.Count);
         }
      }
   }
}
