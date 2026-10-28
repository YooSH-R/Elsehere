namespace A305.Api
{
    /// <summary>multipart 요청에 담을 파일 하나.</summary>
    public sealed class ApiFile
    {
        public ApiFile(string fieldName, string fileName, string contentType, byte[] bytes)
        {
            FieldName = fieldName;
            FileName = fileName;
            ContentType = contentType;
            Bytes = bytes;
        }

        /// <summary>폼 칸 이름. 예: <c>audio</c></summary>
        public string FieldName { get; }

        public string FileName { get; }

        /// <summary>파일 형식. 예: <c>audio/wav</c></summary>
        public string ContentType { get; }

        public byte[] Bytes { get; }
    }
}
