namespace EzLink.Protocol
{
    /// <summary>
    /// Base of every message. A message knows its command set / command number and how to
    /// write its payload; each subclass has a static <c>Read(PayloadReader)</c> for the reverse
    /// and <see cref="MessageFactory"/> picks the right one from a received frame.
    /// Message fields are public so callers can fill them in directly.
    /// </summary>
    public abstract class Message
    {
        private readonly byte commandSet;
        private readonly byte command;

        protected Message(byte commandSet, byte command)
        {
            this.commandSet = commandSet;
            this.command = command;
        }

        public byte CommandSet
        {
            get { return commandSet; }
        }

        public byte Command
        {
            get { return command; }
        }

        protected abstract void WritePayload(PayloadWriter writer);

        /// <summary>Encodes the message into a frame. Replies echo the request's id; unsolicited messages use 0.</summary>
        public Frame ToFrame(uint requestId)
        {
            PayloadWriter writer = new PayloadWriter();
            WritePayload(writer);
            return new Frame(commandSet, command, requestId, writer.ToArray());
        }
    }
}
