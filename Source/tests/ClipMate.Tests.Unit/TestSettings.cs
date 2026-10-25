// A test that hangs (a deadlock, a wait that never ends) fails after a minute with its name, instead of stalling the
// whole run with no output. A slower test can raise its own limit with [Timeout].
[assembly: Timeout(60_000)]
