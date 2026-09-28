What's New in Luminark v1.1.15:

• Runtime Memory Reduction & Performance Optimization:
  - Working Set Trimming: Resolved an issue where idle background memory could reach ~150-200 MB. Re-implemented an intelligent working set trim combining full Gen2 GC, LOH compaction, and Win32 process working set trimming, bringing idle tray memory down to ~15-30 MB.
  - Startup Buffer Cleanup: Automatically sheds temporary XAML compilation, DirectX buffers, and monitor enumeration allocations 3 seconds after launching minimized to the tray.
  - Periodic Idle Maintenance: Added background idle memory maintenance every 10 minutes when all windows are closed, ensuring memory remains consistently lean over multi-day runtimes.
  - .NET 8 DATAS Enabled: Activated Dynamic Adaptation to Application Sizes (DATAS) garbage collection, tuning heap sizes dynamically to minimize memory footprint.
