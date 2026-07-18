# External process adapters

Use this reference only when the legacy application starts child processes. Do not introduce
process execution as a standard migration dependency.

## Resolution

Define a product-specific policy for locating or configuring the executable. The policy may use
explicit configuration, environment discovery, platform conventions, or packaging, but it must
not silently change semantics between supported environments.

## Execution contract

- Pass an executable and argument list without shell concatenation.
- Define working directory, environment, input/output encoding, and maximum captured output.
- Support cancellation and timeout, and clean up the process tree according to product policy.
- Distinguish resolution failure, start failure, non-success exit, timeout, cancellation, and
  malformed results.
- Document when a fallback is allowed; do not turn application errors into silent fallback.

## Testing

Use a fake process adapter for deterministic failure paths. Add focused integration coverage
only for process behavior that cannot be represented by the fake.
