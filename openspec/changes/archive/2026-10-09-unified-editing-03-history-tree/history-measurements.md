# History Measurements

The representative run used the `net10.0` Core test project in Debug mode on Windows x64. It created 1,200 one-chapter edits and retained 1,201 nodes. It then undid the full path and navigated to the leaf and root.

The commit loop allocated 6,279,992 bytes and took 20 ms. The full sequence allocated 12,654,704 bytes. This includes 1,200 commits, 1,200 undos, and two full-depth navigations. Root navigation took 17,798 stopwatch ticks at 10,000,000 ticks per second, or about 1.8 ms.

These figures include the transaction idempotency cache and temporary reconstruction allocations. They measure total allocation, not retained heap size or the retained size of history deltas alone. They describe one local run and do not define performance thresholds.
