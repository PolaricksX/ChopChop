namespace UserService.IntegrationTests.Support;

// Serilog request logging writes via the static Log.Logger. Parallel WebApplicationFactory hosts
// can replace this shared static logger, causing events from one host to be emitted through another.
[CollectionDefinition("Serilog static logger", DisableParallelization = true)]
public sealed class SerilogStaticLoggerCollection;
