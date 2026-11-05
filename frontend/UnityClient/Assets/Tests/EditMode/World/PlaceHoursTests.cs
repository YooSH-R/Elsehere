using System;
using NUnit.Framework;

namespace A305.World.Tests
{
    public class PlaceHoursTests
    {
        // 실제 파일(Data/World/PlaceHours.json)과 같은 모양이다. NIGHT_MARKET은 자정을 넘기는 영업을 보려고 넣었다.
        private const string Json =
            "{\"generatedBy\":\"ai/tools/npc_life_export.py\",\"places\":["
            + "{\"code\":\"CAFE\",\"alwaysOpen\":false,\"from\":\"07:00\",\"to\":\"21:00\"},"
            + "{\"code\":\"HOTEL\",\"alwaysOpen\":true,\"from\":\"\",\"to\":\"\"},"
            + "{\"code\":\"NIGHT_MARKET\",\"alwaysOpen\":false,\"from\":\"18:00\",\"to\":\"02:00\"}"
            + "]}";

        private PlaceHours hours;

        [SetUp]
        public void ReadHours()
        {
            hours = PlaceHours.FromJson(Json);
        }

        [Test]
        public void IsOpen_BetweenOpeningAndClosing_IsOpen()
        {
            Assert.That(hours.IsOpen("CAFE", At(12, 30)), Is.True);
        }

        [Test]
        public void IsOpen_FromTheOpeningMinute_IsOpen()
        {
            Assert.That(hours.IsOpen("CAFE", At(6, 59)), Is.False);
            Assert.That(hours.IsOpen("CAFE", At(7, 0)), Is.True);
        }

        [Test]
        public void IsOpen_FromTheClosingMinute_IsClosed()
        {
            Assert.That(hours.IsOpen("CAFE", At(20, 59)), Is.True);
            Assert.That(hours.IsOpen("CAFE", At(21, 0)), Is.False);
            Assert.That(hours.IsOpen("CAFE", At(23, 59)), Is.False);
        }

        [Test]
        public void IsOpen_AlwaysOpenPlace_IsOpenAllDay()
        {
            Assert.That(hours.IsOpen("HOTEL", At(0, 0)), Is.True);
            Assert.That(hours.IsOpen("HOTEL", At(3, 0)), Is.True);
            Assert.That(hours.IsOpen("HOTEL", At(23, 59)), Is.True);
        }

        [Test]
        public void IsOpen_PlaceWithoutHours_IsAlwaysOpen()
        {
            // 플레이어의 집(HOME)처럼 영업시간이 없는 장소는 닫지 않는다.
            Assert.That(hours.IsOpen("HOME", At(3, 0)), Is.True);
            Assert.That(hours.IsOpen(null, At(3, 0)), Is.True);
        }

        [Test]
        public void IsOpen_HoursCrossingMidnight_StayOpenUntilClosingNextDay()
        {
            Assert.That(hours.IsOpen("NIGHT_MARKET", At(17, 59)), Is.False);
            Assert.That(hours.IsOpen("NIGHT_MARKET", At(18, 0)), Is.True);
            Assert.That(hours.IsOpen("NIGHT_MARKET", At(23, 59)), Is.True);
            Assert.That(hours.IsOpen("NIGHT_MARKET", At(0, 0)), Is.True);
            Assert.That(hours.IsOpen("NIGHT_MARKET", At(1, 59)), Is.True);
            Assert.That(hours.IsOpen("NIGHT_MARKET", At(2, 0)), Is.False);
            Assert.That(hours.IsOpen("NIGHT_MARKET", At(12, 0)), Is.False);
        }

        [Test]
        public void HoursText_ShowsOpeningAndClosingTimes()
        {
            Assert.That(hours.HoursText("CAFE"), Is.EqualTo("07:00–21:00"));
            Assert.That(hours.HoursText("NIGHT_MARKET"), Is.EqualTo("18:00–02:00"));
        }

        [Test]
        public void HoursText_PlaceThatNeverCloses_IsEmpty()
        {
            Assert.That(hours.HoursText("HOTEL"), Is.Null);
            Assert.That(hours.HoursText("HOME"), Is.Null);
        }

        [Test]
        public void AlwaysOpen_OpensEveryPlace()
        {
            // 영업시간을 받기 전(영업시간 처리가 없는 Sandbox 씬)에는 모든 장소에 들어간다.
            Assert.That(PlaceHours.AlwaysOpen.IsOpen("CAFE", At(23, 0)), Is.True);
        }

        [TestCase("7:00")]
        [TestCase("24:00")]
        [TestCase("07:60")]
        [TestCase("")]
        public void FromJson_WithMalformedTime_Throws(string time)
        {
            string json =
                "{\"places\":[{\"code\":\"CAFE\",\"alwaysOpen\":false,\"from\":\""
                + time
                + "\",\"to\":\"21:00\"}]}";

            Assert.That(() => PlaceHours.FromJson(json), Throws.InstanceOf<FormatException>());
        }

        private static int At(int hour, int minute) => hour * 60 + minute;
    }
}
