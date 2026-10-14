# History Measurements

The scale run used the Core test project in Debug mode on Windows x64 with .NET 10.0.12. It used 32 edits per branch and varied chapter count and retained branch count. It measured allocations and build time while creating branches. It measured 32 undo and redo operations. It measured retained process heap after a full collection. It then timed navigation from the last branch to the first branch. With one branch, the navigation measurement covers root-to-leaf navigation because there is no alternate branch.

| Chapters | Branches | Retained nodes | Allocated bytes | Retained heap bytes after full GC | Build and branch ms | Undo 32 µs | Redo 32 µs | Branch navigation µs |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 1 | 33 | 143,872 | 2,578,560 | 9 | 11,932.3 | 416.7 | 235.9 |
| 1 | 4 | 129 | 799,312 | 2,782,856 | 1 | 129.4 | 83.1 | 97.0 |
| 1 | 8 | 257 | 1,764,488 | 3,182,360 | 2 | 285.8 | 103.7 | 102.6 |
| 100 | 1 | 33 | 488,096 | 3,090,344 | 0 | 656.0 | 644.5 | 732.5 |
| 100 | 4 | 129 | 2,766,864 | 2,944,344 | 5 | 850.9 | 696.0 | 1,274.4 |
| 100 | 8 | 257 | 5,893,128 | 3,563,192 | 12 | 730.5 | 663.3 | 1,220.0 |
| 1,000 | 1 | 33 | 3,662,544 | 3,720,944 | 3 | 7,447.9 | 5,440.3 | 5,305.7 |
| 1,000 | 4 | 129 | 20,981,952 | 4,400,928 | 32 | 7,081.5 | 5,467.7 | 11,721.1 |
| 1,000 | 8 | 257 | 44,158,216 | 7,001,672 | 69 | 5,619.2 | 5,291.9 | 15,958.3 |

The run retained all 257 nodes at the largest branch count. For 1,000-chapter documents, branch navigation took 5.3 ms from root to leaf with one branch, 11.7 ms between four branches, and 16.0 ms between eight branches. Allocation and retained heap increased with document size and branch count. Retained heap is a process-wide approximation. It includes the test runner and runtime, so it is not an isolated history-tree size. The first row includes runtime warm-up and the measurements are not performance thresholds.
