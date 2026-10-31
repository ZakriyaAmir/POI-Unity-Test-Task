using NUnit.Framework;
using Poi.Core;

namespace Poi.Core.Tests
{
    public class MockSocketFeedTests
    {
        static PoiStore NewStore() => new PoiStore(new SpatialGrid(-8000f, 16000f, 250f));

        [Test]
        public void Messages_ChangeTheStore()
        {
            var store = NewStore();
            var feed = new MockSocketFeed();
            store.Attach(feed);

            feed.ReceiveMessage("add;1;Cafe;2;10.5;20;icon_2;3");
            feed.ReceiveMessage("move;1;30;40");
            feed.ReceiveMessage("change;1;Cafe 2;2;30;40;icon_2;5");
            Assert.AreEqual(0, store.Count); // nothing happens until processed
            Assert.AreEqual(3, feed.ProcessMessages());

            Assert.IsTrue(store.TryGet(1, out var poi));
            Assert.AreEqual("Cafe 2", poi.Name);
            Assert.AreEqual(30f, poi.X);
            Assert.AreEqual(5, poi.Priority);

            feed.ReceiveMessage("remove;1");
            feed.ProcessMessages();
            Assert.AreEqual(0, store.Count);
        }

        [Test]
        public void BrokenMessage_IsSkipped()
        {
            var feed = new MockSocketFeed();
            feed.ReceiveMessage("hello");
            feed.ReceiveMessage("move;1");
            feed.ProcessMessages();
            Assert.AreEqual(2, feed.BadMessageCount);
        }

        [Test]
        public void MaxPerFrame_LeavesRestForLater()
        {
            var feed = new MockSocketFeed { MaxMessagesPerFrame = 2 };
            for (int i = 0; i < 5; i++) feed.ReceiveMessage("remove;" + i);
            Assert.AreEqual(2, feed.ProcessMessages());
            Assert.AreEqual(3, feed.WaitingCount);
        }

        [Test]
        public void FakeServer_FillsStore()
        {
            var store = NewStore();
            var feed = new MockSocketFeed();
            store.Attach(feed);
            var server = new FakePoiServer(feed) { StartCount = 100, AddsPerSecond = 10, RemovesPerSecond = 0, MovesPerSecond = 10 };

            server.SendStartingList();
            server.Tick(1f);
            feed.ProcessMessages();

            Assert.AreEqual(110, store.Count);
            Assert.AreEqual(0, feed.BadMessageCount);
        }
    }
}
