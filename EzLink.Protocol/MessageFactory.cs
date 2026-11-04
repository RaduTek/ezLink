namespace EzLink.Protocol
{
    /// <summary>Turns a received <see cref="Frame"/> back into a typed <see cref="Message"/>.</summary>
    public static class MessageFactory
    {
        /// <summary>
        /// Returns the message for the frame, or null if the command set or command is unknown
        /// (the receiver should answer with <see cref="UnsupportedMessage"/>). Throws
        /// <see cref="ProtocolException"/> if the command is known but its payload is malformed
        /// or has trailing bytes.
        /// </summary>
        public static Message Parse(Frame frame)
        {
            PayloadReader r = new PayloadReader(frame.Payload);
            Message message;

            switch (frame.CommandSet)
            {
                case CommandSets.General:
                    message = ParseGeneral(frame.Command, r);
                    break;
                case CommandSets.Files:
                    message = ParseFiles(frame.Command, r);
                    break;
                default:
                    return null;
            }

            if (message != null)
            {
                r.EnsureEnd();
            }
            return message;
        }

        private static Message ParseGeneral(byte command, PayloadReader r)
        {
            switch (command)
            {
                case GeneralCommands.Hello: return HelloMessage.Read(r);
                case GeneralCommands.HelloReply: return HelloReplyMessage.Read(r);
                case GeneralCommands.Ping: return PingMessage.Read(r);
                case GeneralCommands.Pong: return PongMessage.Read(r);
                case GeneralCommands.Ok: return OkMessage.Read(r);
                case GeneralCommands.Error: return ErrorMessage.Read(r);
                case GeneralCommands.Cancel: return CancelMessage.Read(r);
                case GeneralCommands.TrustChanged: return TrustChangedMessage.Read(r);
                case GeneralCommands.Unsupported: return UnsupportedMessage.Read(r);
                default: return null;
            }
        }

        private static Message ParseFiles(byte command, PayloadReader r)
        {
            switch (command)
            {
                case FileCommands.GetRoots: return GetRootsRequest.Read(r);
                case FileCommands.Roots: return RootsReply.Read(r);
                case FileCommands.ListDir: return ListDirRequest.Read(r);
                case FileCommands.DirListing: return DirListingReply.Read(r);
                case FileCommands.ListTree: return ListTreeRequest.Read(r);
                case FileCommands.TreeEntries: return TreeEntriesReply.Read(r);
                case FileCommands.TreeEnd: return TreeEndReply.Read(r);
                case FileCommands.Stat: return StatRequest.Read(r);
                case FileCommands.StatResult: return StatResultReply.Read(r);
                case FileCommands.ReadFile: return ReadFileRequest.Read(r);
                case FileCommands.FileData: return FileDataReply.Read(r);
                case FileCommands.FileEnd: return FileEndReply.Read(r);
                case FileCommands.WriteBegin: return WriteBeginRequest.Read(r);
                case FileCommands.WriteData: return WriteDataRequest.Read(r);
                case FileCommands.WriteEnd: return WriteEndRequest.Read(r);
                case FileCommands.CreateDir: return CreateDirRequest.Read(r);
                case FileCommands.Delete: return DeleteRequest.Read(r);
                case FileCommands.Rename: return RenameRequest.Read(r);
                default: return null;
            }
        }
    }
}
