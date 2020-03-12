using System.Collections.Generic;
using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;

namespace MockFramework
{
    public class ThingCache
    {
        private readonly IDictionary<string, Thing> dictionary
            = new Dictionary<string, Thing>();

        private readonly IThingService thingService;

        public ThingCache(IThingService thingService)
        {
            this.thingService = thingService;
        }

        public Thing Get(string thingId)
        {
            Thing thing;
            if (dictionary.TryGetValue(thingId, out thing))
                return thing;
            if (thingService.TryRead(thingId, out thing))
            {
                dictionary[thingId] = thing;
                return thing;
            }

            return null;
        }
    }

    [TestFixture]
    public class ThingCache_Should
    {
        private IThingService thingService;
        private ThingCache thingCache;

        private const string thingId1 = "TheDress";
        private Thing thing1 = new Thing(thingId1);

        private const string thingId2 = "CoolBoots";
        private Thing thing2 = new Thing(thingId2);

        [SetUp]
        public void SetUp()
        {
            thingService = A.Fake<IThingService>();
            thingCache = new ThingCache(thingService);
        }

        [TearDown]
        public void TearDown()
        {
            thingService = null;
            thingCache = null;
        }

        [Test]
        public void ReturnCorrectThing_TakeItFirstTimeFromService()
        {
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).Returns(true);
            
            thingCache.Get(thingId1).Should().Be(thing1);
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).MustHaveHappened();
        }

        [Test]
        public void Get_NonExistingObject_ReturnsNull()
        {
            thingCache.Get(thingId1).Should().BeNull();
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).MustHaveHappened();
        }

        [Test]
        public void DoestInvokeServiceMethod_OnSecondTime()
        {
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).Returns(true);
            thingCache.Get(thingId1);
            thingCache.Get(thingId1);
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void ReturnNull_OnEmptyString_IfNotExistEqualId()
        {
            thingCache.Get("").Should().BeNull();
        }

        [Test]
        public void ReturnCorrectThing_OnMoreThanOneCaches()
        {
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).Returns(true);
            A.CallTo(() => thingService.TryRead(thingId2, out thing2)).Returns(true);
            thingCache.Get(thingId1).Should().Be(thing1);
            thingCache.Get(thingId2).Should().Be(thing2);
        }

        [Test]
        public void ReturnZero_OnNonExistId()
        {
            const string someId = "srgdfgh";
            thingCache.Get(someId).Should().Be(null);
            Thing nullThing = null;
            A.CallTo(() => thingService.TryRead(someId, out nullThing)).MustHaveHappened(Repeated.Exactly.Once);

        }

        [Test]
        public void ReturnSameThings_OnMoreThanOneGetMethodInvoke()
        {
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).Returns(true);
            var thing = thingCache.Get(thingId1);
            thingCache.Get(thingId1).Should().Be(thing);
        }

        [Test]
        public void SupportsNullThing_IfIdExists()
        {
            const string id = "esd";
            Thing thing = null;
            A.CallTo(() => thingService.TryRead(id, out thing)).Returns(true);

            thingCache.Get(id).Should().BeNull();
            thingCache.Get(id).Should().BeNull();

            A.CallTo(() => thingService.TryRead(id, out thing)).MustHaveHappened(Repeated.Exactly.Once);
        }
    }
}