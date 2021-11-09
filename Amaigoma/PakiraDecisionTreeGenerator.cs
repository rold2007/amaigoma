namespace Amaigoma
{
    using MathNet.Numerics;
    using MathNet.Numerics.Distributions;
    using MathNet.Numerics.LinearAlgebra.Double;
    using MathNet.Numerics.Statistics;
    using Shouldly;
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    // Create a separate source file for this
    public static class IEnumerableExtensions
    {
        // Obtained from https://stackoverflow.com/a/1287572/263228
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Random rng)
        {
            T[] elements = source.ToArray();
            for (int i = elements.Length - 1; i >= 0; i--)
            {
                // Swap element "i" with a random earlier element it (or itself)
                // ... except we don't really need to swap it fully, as we can
                // return it immediately, and afterwards it's irrelevant.
                int swapIndex = rng.Next(i + 1);
                yield return elements[swapIndex];
                elements[swapIndex] = elements[i];
            }
        }
    }

    public sealed record TrainData
    {
        public ImmutableList<ImmutableList<double>> Samples { get; } = ImmutableList<ImmutableList<double>>.Empty;
        public ImmutableList<double> Labels { get; } = ImmutableList<double>.Empty;

        public TrainData()
        {
        }

        public TrainData(ImmutableList<ImmutableList<double>> samples, ImmutableList<double> labels)
        {
            Samples = samples;
            Labels = labels;
        }

        public TrainData AddSample(IEnumerable<double> sample, double label)
        {
            ImmutableList<double> immutableSample = sample.ToImmutableList();

            if (!Samples.IsEmpty)
            {
                immutableSample.Count.ShouldBe(Samples[0].Count);
            }

            return new TrainData(Samples.Add(immutableSample), Labels.Add(label));
        }
    }

    public sealed record TrainDataCache
    {
        public ImmutableList<SabotenCache> Samples { get; } = ImmutableList<SabotenCache>.Empty;
        public ImmutableList<double> Labels { get; } = ImmutableList<double>.Empty;

        public TrainDataCache(ImmutableList<SabotenCache> samples, ImmutableList<double> labels)
        {
            Samples = samples;
            Labels = labels;
        }

        public TrainDataCache AddSamples(TrainDataCache samples)
        {
            samples.Samples.Count.ShouldBeGreaterThan(0);
            samples.Labels.Count.ShouldBeGreaterThan(0);

            if (!Samples.IsEmpty)
            {
                samples.Samples[0].Data.Count.ShouldBe(Samples[0].Data.Count);
            }

            return new TrainDataCache(Samples.AddRange(samples.Samples), Labels.AddRange(samples.Labels));
        }
    }

    public class PakiraDecisionTreeGenerator
    {
        static public readonly int UNKNOWN_CLASS_INDEX = -1;
        static public readonly int INSUFFICIENT_SAMPLES_CLASS_INDEX = -2;
        static public readonly int randomSeed = new Random().Next();
        static private readonly int MINIMUM_SAMPLE_COUNT = 1000;
        static private readonly double DEFAULT_CERTAINTY_SCORE = 0.95;
        static private readonly PassThroughTransformer DefaultDataTransformer = new PassThroughTransformer();
        private readonly Random RandomSource = new Random(randomSeed);
        private readonly DiscreteUniform discreteUniform;

        public PakiraDecisionTreeGenerator()
        {
            MinimumSampleCount = MINIMUM_SAMPLE_COUNT;
            CertaintyScore = DEFAULT_CERTAINTY_SCORE;
            discreteUniform = new DiscreteUniform(0, 255, RandomSource);
        }

        public int MinimumSampleCount { get; set; }

        public double CertaintyScore { get; set; }

        public PakiraDecisionTreeModel Generate(PakiraDecisionTreeModel pakiraDecisionTreeModel, TrainData trainData)
        {
            const int sampleSize = 100;
            const int minimumValidSampleCount = (int)(0.20 * sampleSize);
            ImmutableList<double> trainSample = trainData.Samples[0];
            int featureCount = trainSample.Count();
            bool generateMoreData = true;
            ImmutableList<SabotenCache> trainSamplesCache = trainData.Samples.Select(d => new SabotenCache(d)).ToImmutableList();
            ImmutableList<double> immutableTrainLabels = trainData.Labels;

            if (pakiraDecisionTreeModel.Tree.Root == null)
            {
                PakiraLeaf initialLeaf = new PakiraLeaf(INSUFFICIENT_SAMPLES_CLASS_INDEX);

                pakiraDecisionTreeModel = pakiraDecisionTreeModel.UpdateTree(PakiraTree.Empty.AddLeaf(initialLeaf));
                pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataDistributionSamplesCache(initialLeaf, ImmutableList<SabotenCache>.Empty);
            }

            for (int trainSampleIndex = 0; trainSampleIndex < trainSamplesCache.Count; trainSampleIndex++)
            {
                PakiraLeaf predictedLeaf = pakiraDecisionTreeModel.PredictNode(trainSamplesCache[trainSampleIndex]);

                pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(predictedLeaf, new TrainDataCache(ImmutableList<SabotenCache>.Empty.Add(trainSamplesCache[trainSampleIndex]), ImmutableList<double>.Empty.Add(immutableTrainLabels[trainSampleIndex])));
            }

            while (generateMoreData)
            {
                pakiraDecisionTreeModel = BuildTree(pakiraDecisionTreeModel);

                List<PakiraLeaf> insufficientSamplesLeaves = pakiraDecisionTreeModel.Tree.GetNodes().FindAll(pakiraNode => pakiraNode.IsLeaf && pakiraNode.Value == INSUFFICIENT_SAMPLES_CLASS_INDEX).Cast<PakiraLeaf>().ToList();
                ImmutableList<SabotenCache> newSamplesCache = ImmutableList<SabotenCache>.Empty;

                foreach (PakiraLeaf leaf in insufficientSamplesLeaves)
                {
                    ImmutableList<SabotenCache> dataDistributionSamplesCache = pakiraDecisionTreeModel.DataDistributionSamplesCache(leaf);
                    int samplesCount = dataDistributionSamplesCache.Count();
                    int newValidSampleCount = 0;
                    int invalidSampleCount = 0;
                    double randomProportion = 100.0;

                    while (newValidSampleCount < MinimumSampleCount)
                    {
                        for (int i = 0; i < sampleSize; i++)
                        {
                            SabotenCache newSampleCache;

                            if (randomProportion < 100.0)
                            {
                                int dataSampleIndex = RandomSource.Next(samplesCount);
                                SabotenCache dataSample = dataDistributionSamplesCache[dataSampleIndex];

                                newSampleCache = new SabotenCache(DenseVector.Create(featureCount, (featureIndex) =>
                                {
                                    if (RandomSource.NextDouble() * 100 < randomProportion)
                                    {
                                        return discreteUniform.Sample();
                                    }
                                    else
                                    {
                                        dataSample = pakiraDecisionTreeModel.Prefetch(dataSample, featureIndex);

                                        return dataSample[featureIndex];
                                    }
                                }
                                ));
                            }
                            else
                            {
                                newSampleCache = new SabotenCache(DenseVector.Create(featureCount, (i) => discreteUniform.Sample()));
                            }

                            IPakiraNode predictedNode = pakiraDecisionTreeModel.PredictNode(newSampleCache);
                            double predictedValue = predictedNode.Value;

                            if (predictedNode == leaf)
                            {
                                newSamplesCache = newSamplesCache.Add(newSampleCache);
                                newValidSampleCount++;
                            }
                            else
                            {
                                invalidSampleCount++;
                            }
                        }

                        if (newValidSampleCount < minimumValidSampleCount)
                        {
                            randomProportion -= 10;

                            // Don't let fully-unchanged new samples
                            randomProportion = Math.Max(randomProportion, 10);
                        }
                    }

                    pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataDistributionSamplesCache(leaf, newSamplesCache);
                }

                generateMoreData = insufficientSamplesLeaves.Count() > 0;
            }

            return pakiraDecisionTreeModel;
        }

        static private bool ThresholdCompareLessThanOrEqual(double inputValue, double threshold)
        {
            return inputValue <= threshold;
        }

        private struct ProcessNode
        {
            public ProcessNode(IPakiraNode node, TrainDataCache trainSamplesCache, ImmutableList<SabotenCache> dataDistributionSamplesCache)
            {
                Node = node;
                TrainSamplesCache = trainSamplesCache;
                DataDistributionSamplesCache = dataDistributionSamplesCache;
            }

            public IPakiraNode Node;
            public TrainDataCache TrainSamplesCache;
            public ImmutableList<SabotenCache> DataDistributionSamplesCache;
        };

        private PakiraDecisionTreeModel BuildTree(PakiraDecisionTreeModel pakiraDecisionTreeModel)
        {
            Stack<ProcessNode> processNodes = new Stack<ProcessNode>();
            PakiraTree pakiraTree = pakiraDecisionTreeModel.Tree;

            {
                List<PakiraLeaf> insufficientSamplesLeaves = pakiraDecisionTreeModel.Tree.GetNodes().FindAll(pakiraNode => pakiraNode.IsLeaf && pakiraNode.Value == INSUFFICIENT_SAMPLES_CLASS_INDEX).Cast<PakiraLeaf>().ToList();

                foreach (PakiraLeaf pakiraLeaf in insufficientSamplesLeaves)
                {
                    processNodes.Push(new ProcessNode(pakiraLeaf, pakiraDecisionTreeModel.TrainDataCache(pakiraLeaf), pakiraDecisionTreeModel.DataDistributionSamplesCache(pakiraLeaf)));
                }
            }

            PakiraLeaf[] leaves = new PakiraLeaf[2];
            ImmutableList<SabotenCache>[] sampleSliceCache = new ImmutableList<SabotenCache>[2];
            ImmutableList<SabotenCache>[] slice = new ImmutableList<SabotenCache>[2];
            ImmutableList<double>[] ySlice = new ImmutableList<double>[2];

            while (processNodes.Count > 0)
            {
                ProcessNode processNode = processNodes.Pop();

                ImmutableList<SabotenCache> processNodeDataDistributionSamplesCache = processNode.DataDistributionSamplesCache;

                if (processNodeDataDistributionSamplesCache.Count() >= MinimumSampleCount)
                {
                    TrainDataCache processNodeTrainSamplesCache = processNode.TrainSamplesCache;
                    ImmutableList<SabotenCache> extractedDataDistributionSamplesCache = processNodeDataDistributionSamplesCache.Take(MinimumSampleCount).ToImmutableList();
                    ImmutableList<SabotenCache> remainingDataDistributionSamplesCache = processNodeDataDistributionSamplesCache.Skip(MinimumSampleCount).ToImmutableList();

                    Tuple<int, double, ImmutableList<SabotenCache>, ImmutableList<SabotenCache>> tuple = GetBestSplit(extractedDataDistributionSamplesCache, processNodeTrainSamplesCache.Samples, pakiraDecisionTreeModel);
                    int bestFeatureIndex = tuple.Item1;
                    double threshold = tuple.Item2;
                    ImmutableList<SabotenCache> bestSplitDataDistributionSamplesCache = tuple.Item3;
                    ImmutableList<SabotenCache> bestSplitTrainSamplesCache = tuple.Item4;

                    remainingDataDistributionSamplesCache = pakiraDecisionTreeModel.Prefetch(remainingDataDistributionSamplesCache, bestFeatureIndex);

                    ImmutableList<SabotenCache> concatenatedDataDistributionSamples = bestSplitDataDistributionSamplesCache.Concat(remainingDataDistributionSamplesCache).ToImmutableList();

                    concatenatedDataDistributionSamples.Count().ShouldBeGreaterThan(0);

                    PakiraNode node = new PakiraNode(bestFeatureIndex, threshold);

                    for (int leafIndex = 0; leafIndex < 2; leafIndex++)
                    {
                        bool theKey = (leafIndex == 0);

                        sampleSliceCache[leafIndex] = concatenatedDataDistributionSamples.Where(column => ThresholdCompareLessThanOrEqual(column[bestFeatureIndex], threshold) == theKey).ToImmutableList();

                        slice[leafIndex] = bestSplitTrainSamplesCache.Where(column => ThresholdCompareLessThanOrEqual(column[bestFeatureIndex], threshold) == theKey).ToImmutableList();

                        ySlice[leafIndex] = processNodeTrainSamplesCache.Labels.Where(
                                    (trainLabel, trainLabelIndex) =>
                                    {
                                        double trainSample = bestSplitTrainSamplesCache.ElementAt(trainLabelIndex)[bestFeatureIndex];

                                        return ThresholdCompareLessThanOrEqual(trainSample, threshold) == theKey;
                                    }
                                    ).ToImmutableList();

                        sampleSliceCache[leafIndex].Count().ShouldNotBe(concatenatedDataDistributionSamples.Count());
                    }

                    for (int leafIndex = 0; leafIndex < 2; leafIndex++)
                    {
                        if (slice[leafIndex].Count() > 0)
                        {
                            int distinctLabelsCount = ySlice[leafIndex].Distinct().Count();

                            // only one answer, set leaf
                            if (distinctLabelsCount == 1)
                            {
                                double leafValue = ySlice[leafIndex].First();

                                leaves[leafIndex] = new PakiraLeaf(leafValue);
                            }
                            // otherwise continue to build tree
                            else
                            {
                                leaves[leafIndex] = new PakiraLeaf(UNKNOWN_CLASS_INDEX);

                                processNodes.Push(new ProcessNode(leaves[leafIndex], new TrainDataCache(slice[leafIndex], ySlice[leafIndex]), sampleSliceCache[leafIndex]));
                            }
                        }
                        else
                        {
                            // We don't have any training data for this node
                            leaves[leafIndex] = new PakiraLeaf(UNKNOWN_CLASS_INDEX);
                        }
                    }

                    pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveDataDistributionSamplesCache(processNode.Node as PakiraLeaf);
                    pakiraTree = pakiraTree.ReplaceLeaf(processNode.Node as PakiraLeaf, PakiraTree.Empty.AddNode(node, leaves[0], leaves[1]));
                    pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataDistributionSamplesCache(leaves[0], sampleSliceCache[0]);
                    pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataDistributionSamplesCache(leaves[1], sampleSliceCache[1]);
                    pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveTrainDataCache(processNode.Node as PakiraLeaf);
                    pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(leaves[0], new TrainDataCache(slice[0], ySlice[0]));
                    pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(leaves[1], new TrainDataCache(slice[1], ySlice[1]));
                }
                else
                {
                    PakiraLeaf pakiraLeaf = new PakiraLeaf(INSUFFICIENT_SAMPLES_CLASS_INDEX);

                    pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveDataDistributionSamplesCache(processNode.Node as PakiraLeaf);
                    pakiraTree = pakiraTree.ReplaceLeaf(processNode.Node as PakiraLeaf, PakiraTree.Empty.AddLeaf(pakiraLeaf));
                    pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddDataDistributionSamplesCache(pakiraLeaf, processNodeDataDistributionSamplesCache);
                    pakiraDecisionTreeModel = pakiraDecisionTreeModel.RemoveTrainDataCache(processNode.Node as PakiraLeaf);
                    pakiraDecisionTreeModel = pakiraDecisionTreeModel.AddTrainDataCache(pakiraLeaf, processNode.TrainSamplesCache);
                }
            }

            return pakiraDecisionTreeModel.UpdateTree(pakiraTree);
        }

        private Tuple<int, double, ImmutableList<SabotenCache>, ImmutableList<SabotenCache>> GetBestSplit(ImmutableList<SabotenCache> extractedDataDistributionSamplesCache, ImmutableList<SabotenCache> extractedTrainSamplesCache, PakiraDecisionTreeModel pakiraDecisionTreeModel)
        {
            ImmutableList<SabotenCache> extractedDataDistributionSamplesCacheList = extractedDataDistributionSamplesCache.ToImmutableList();
            ImmutableList<int> randomFeatureIndices = pakiraDecisionTreeModel.FeatureIndices().Shuffle(RandomSource).ToImmutableList();

            double bestScore = -1.0;
            int bestFeature = -1;

            foreach (int featureIndex in randomFeatureIndices)
            {
                double score = 0.0;

                extractedDataDistributionSamplesCacheList = pakiraDecisionTreeModel.Prefetch(extractedDataDistributionSamplesCacheList, featureIndex);

                ImmutableList<double> featureDataDistributionSample = extractedDataDistributionSamplesCacheList.Select<SabotenCache, double>(sample =>
                {
                    return sample[featureIndex];
                }
                ).ToImmutableList();

                double count = featureDataDistributionSample.Count();

                Histogram histogram = new Histogram(featureDataDistributionSample, 10);

                // LowerBound always has an offset
                histogram.LowerBound.ShouldBeGreaterThanOrEqualTo((0.0).Decrement());
                histogram.UpperBound.ShouldBeLessThanOrEqualTo(255.0);

                extractedTrainSamplesCache = pakiraDecisionTreeModel.Prefetch(extractedTrainSamplesCache, featureIndex);

                score = extractedTrainSamplesCache.Max((SabotenCache trainSample) =>
                {
                    double trainSampleValue = trainSample[featureIndex];

                    trainSampleValue.ShouldBeGreaterThanOrEqualTo(0.0);
                    trainSampleValue.ShouldBeLessThanOrEqualTo(255.0);

                    // LowerBound is not included in bucket
                    if ((trainSampleValue > histogram.LowerBound) && (trainSampleValue <= histogram.UpperBound))
                    {
                        return count - histogram.GetBucketOf(trainSampleValue).Count;
                    }
                    else
                    {
                        // We might also end up here when the training sample falls between
                        // the last tree split and the min/max of the distribution samples.
                        // In that case it is not a very good split but this should happen
                        // very rarely and it is not worse than a random split.
                        return count;
                    }
                }
                );

                score /= count;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestFeature = featureIndex;
                }

                if (score >= CertaintyScore)
                {
                    break;
                }
            }

            bestFeature.ShouldBeGreaterThanOrEqualTo(0);

            double bestFeatureAverage = extractedDataDistributionSamplesCacheList.Select(sample => sample[bestFeature]).Mean();

            return new Tuple<int, double, ImmutableList<SabotenCache>, ImmutableList<SabotenCache>>(bestFeature, bestFeatureAverage, extractedDataDistributionSamplesCacheList, extractedTrainSamplesCache);
        }
    }
}