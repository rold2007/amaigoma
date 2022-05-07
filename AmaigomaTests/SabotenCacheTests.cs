using Amaigoma;
using Shouldly;
using System;
using Xunit;

namespace AmaigomaTests
{
   public class SabotenCacheTests
   {
      [Fact]
      public void LoadCacheTest()
      {
         SabotenCache sabotenCache = new(new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

         sabotenCache = sabotenCache.LoadCache(new Range(8, 10), new double[] { 8, 9 });
         sabotenCache = sabotenCache.LoadCache(new Range(1, 3), new double[] { 1, 2 });

         sabotenCache[1].ShouldBe(1);
         sabotenCache[2].ShouldBe(2);
         sabotenCache[8].ShouldBe(8);
         sabotenCache[9].ShouldBe(9);
         sabotenCache.CacheHit(0).ShouldBeFalse();
         sabotenCache.CacheHit(1).ShouldBeTrue();
         sabotenCache.CacheHit(2).ShouldBeTrue();
         sabotenCache.CacheHit(3).ShouldBeFalse();
         sabotenCache.CacheHit(4).ShouldBeFalse();
         sabotenCache.CacheHit(5).ShouldBeFalse();
         sabotenCache.CacheHit(6).ShouldBeFalse();
         sabotenCache.CacheHit(7).ShouldBeFalse();
         sabotenCache.CacheHit(8).ShouldBeTrue();
         sabotenCache.CacheHit(9).ShouldBeTrue();
      }
   }
}
