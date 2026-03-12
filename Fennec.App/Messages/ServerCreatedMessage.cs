using System;

namespace Fennec.App.Messages;

public record ServerCreatedMessage(Guid ServerId, string ServerName);
