namespace A305.World
{
    /// <summary>
    /// 경찰관과 한 번 대화하는 동안 신고와 출동할 장소를 모은다. 분명한 신고(<see cref="PoliceReportRequest"/>)를 들으면 접수하고,
    /// 접수한 뒤에 마을 장소를 하나만 말하면 그 장소로 바꾼다("My bag was stolen." → "It was the cafe.").
    /// 순찰차는 대화를 마친 뒤 <see cref="TryTakeDispatch"/>로 꺼낸 장소로 출동한다. 장소가 없으면 본길을 순찰한다.
    /// </summary>
    public sealed class PoliceReportIntake
    {
        public bool HasReport { get; private set; }

        /// <summary>출동할 장소 코드. 아직 장소를 듣지 못했으면 null</summary>
        public string Place { get; private set; }

        public void Hear(string text)
        {
            if (PoliceReportRequest.TryResolve(text, out string reportedPlace))
            {
                HasReport = true;
                if (reportedPlace != null)
                    Place = reportedPlace;
                return;
            }
            if (!HasReport || string.IsNullOrWhiteSpace(text))
                return;
            string named = TaxiDestinationRequest.FindSinglePlace(text.ToLowerInvariant());
            if (named != null && named != TaxiDestinationRequest.PoliceStation)
                Place = named;
        }

        /// <summary>접수한 신고가 있으면 출동할 장소를 꺼내고 다음 대화를 위해 비운다.</summary>
        public bool TryTakeDispatch(out string place)
        {
            place = Place;
            bool reported = HasReport;
            HasReport = false;
            Place = null;
            return reported;
        }
    }
}
