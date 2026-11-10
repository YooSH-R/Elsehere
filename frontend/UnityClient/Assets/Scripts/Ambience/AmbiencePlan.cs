using System;

namespace A305.Ambience
{
    public enum AmbienceBed
    {
        None,
        Room,
        Street,
        Hotel,
        Cafe,
        Restaurant,
        Laundromat,
        Taxi,
        QuietShop,
        Rain,
    }

    public enum AmbienceEffect
    {
        DoorClose,
        HotelBell,
        CarPass,
        TaxiDoorClose,
        ShortHorn,
    }

    public static class AmbiencePlan
    {
        public static AmbienceBed Resolve(string scene, string shop, bool passenger, bool raining)
        {
            string place = (scene ?? "").Replace("Scene", "").ToUpperInvariant();
            if (place == "ENTRY" || place.Length == 0)
                return AmbienceBed.None;
            if (passenger)
                return AmbienceBed.Taxi;
            if (!string.IsNullOrEmpty(shop))
            {
                if (shop == "DINER")
                    return AmbienceBed.Restaurant;
                if (shop == "LAUNDROMAT")
                    return AmbienceBed.Laundromat;
                return AmbienceBed.QuietShop;
            }
            switch (place)
            {
                case "HOME":
                case "ROOM":
                    return AmbienceBed.Room;
                case "TOWN":
                    return raining ? AmbienceBed.Rain : AmbienceBed.Street;
                case "HOTEL":
                    return AmbienceBed.Hotel;
                case "CAFE":
                    return AmbienceBed.Cafe;
                case "SHOP":
                case "POLICESTATION":
                    return AmbienceBed.QuietShop;
                default:
                    return AmbienceBed.None;
            }
        }

        public static float Volume(float normal, bool talking) =>
            Math.Max(0, Math.Min(1, normal)) * (talking ? .15f : 1f);

        public static bool IsStreet(AmbienceBed bed) =>
            bed == AmbienceBed.Street || bed == AmbienceBed.Rain;
    }

    public sealed class VehicleSoundGate
    {
        private bool passArmed = true;
        private double nextPass;
        private double nextHorn;
        private float stoppedSeconds;

        public bool TryPass(double now, float meters, bool moving)
        {
            if (meters > 14)
                passArmed = true;
            if (!moving || meters > 10 || !passArmed || now < nextPass)
                return false;
            passArmed = false;
            nextPass = now + 20;
            return true;
        }

        public bool TryHorn(double now, float meters, bool moving, bool green, float seconds)
        {
            if (moving || meters > 3.4f || !green)
            {
                stoppedSeconds = 0;
                return false;
            }
            stoppedSeconds += Math.Max(0, seconds);
            if (stoppedSeconds < 2 || now < nextHorn)
                return false;
            stoppedSeconds = 0;
            nextHorn = now + 40;
            return true;
        }
    }
}
