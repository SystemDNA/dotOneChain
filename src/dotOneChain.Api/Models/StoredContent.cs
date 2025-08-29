namespace dotOneChain.Api.Models
{
    public class StoredContent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Cid { get; set; } = string.Empty;
        public MongoDB.Bson.ObjectId GridFsId { get; set; }
        public string FileName { get; set; } = "file.bin";
        public long Size { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

}
