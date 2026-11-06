using System;
using System.Text;

namespace A305.Api
{
    /// <summary>
    /// 서버가 보내는 이벤트 스트림(SSE) 글자를 이벤트 단위로 나눈다. 이벤트는 <c>event: 이름</c>, <c>data: 내용</c> 줄 뒤 빈 줄로 끝난다.
    /// 글자는 이벤트 중간에서 끊겨 올 수 있어, 빈 줄이 올 때까지 모아 둔다.
    /// </summary>
    public sealed class ServerSentEventReader
    {
        private readonly Action<string, string> onEvent;
        private readonly StringBuilder pending = new StringBuilder();

        /// <param name="onEvent">이벤트 이름과 data를 받는다.</param>
        public ServerSentEventReader(Action<string, string> onEvent)
        {
            this.onEvent = onEvent;
        }

        /// <summary>받은 글자를 넣는다. 끝난 이벤트는 바로 넘긴다.</summary>
        public void Read(string text)
        {
            pending.Append(text);
            // 줄바꿈은 \n과 \r\n 둘 다 쓸 수 있다. \r 뒤의 \n이 다음 조각에 올 수 있어 모은 글자 전체에서 바꾼다.
            string buffered = pending.ToString().Replace("\r\n", "\n");
            int end;
            while ((end = buffered.IndexOf("\n\n", StringComparison.Ordinal)) >= 0)
            {
                Dispatch(buffered.Substring(0, end));
                buffered = buffered.Substring(end + 2);
            }

            pending.Clear().Append(buffered);
        }

        private void Dispatch(string block)
        {
            string name = "message";
            var data = new StringBuilder();
            foreach (string line in block.Split('\n'))
            {
                if (line.StartsWith("event:", StringComparison.Ordinal))
                {
                    name = line.Substring("event:".Length).Trim();
                }
                else if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    if (data.Length > 0)
                    {
                        data.Append('\n');
                    }

                    data.Append(line.Substring("data:".Length).TrimStart());
                }
            }

            if (data.Length > 0)
            {
                onEvent(name, data.ToString());
            }
        }
    }
}
