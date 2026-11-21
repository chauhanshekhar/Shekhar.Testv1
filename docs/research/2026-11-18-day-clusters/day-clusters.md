# Stop-out clustering by session

Simulated, costed entries (live rules and unfiltered base). SL = T1 never reached and not an end-of-day exit. HINDSIGHT features describe a session only after it ended and cannot be used at entry. FIRST-HOUR features use 09:15-10:15 IST and are scored on entries from 10:20 only.

## CALL|Base|AllFiltersOff | Call (n=1882, pooled SL 61%)
Clustering: 168 session-symbols with >=4 trades; SD of daily SL share 0.157 vs 0.162 expected if independent; ratio 0.97 (>1 = clustered).
Worst sessions (>=6 trades): SENSEX 09-03 Normal/TrendDown 9/10 SL net -11613; SENSEX 11-18 Normal/TrendDown 9/10 SL net -11417; SENSEX 11-11 GapUp/Reversal 9/10 SL net -6117; BANKNIFTY 08-07 GapDown/Continuation 7/8 SL net -6917; BANKNIFTY 08-18 Normal/Range 7/8 SL net -6614; SENSEX 09-10 Normal/TrendUp 7/8 SL net -5683; BANKNIFTY 07-15 GapUp/Range 13/15 SL net -22781; SENSEX 07-16 Normal/Range 13/15 SL net -12385

### HINDSIGHT: day type (opening/path)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| Any/Unknown | 26 | 2 | 62% | 62% | - | -366 |
| GapDown/Continuation | 71 | 10 | 73% | 74% | - | -715 |
| GapDown/Range | 55 | 7 | 58% | 67% | 55% | 99 |
| GapDown/Reversal | 123 | 11 | 56% | 54% | 64% | 119 |
| GapUp/Continuation | 202 | 17 | 55% | 65% | 52% | 186 |
| GapUp/Range | 188 | 19 | 63% | 63% | 62% | -325 |
| GapUp/Reversal | 72 | 14 | 67% | 63% | 72% | -410 |
| Normal/Range | 387 | 43 | 63% | 64% | 61% | -124 |
| Normal/TrendDown | 285 | 40 | 71% | 71% | 69% | -585 |
| Normal/TrendUp | 473 | 42 | 54% | 62% | 51% | 250 |

### HINDSIGHT: full-day range % (tercile within symbol)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 636 | 78 | 66% | 67% | 59% | -380 |
| 2 mid | 623 | 66 | 63% | 64% | 61% | -245 |
| 3 high | 623 | 61 | 54% | 56% | 54% | 328 |

### HINDSIGHT: full-day efficiency |close-open|/range (tercile)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 644 | 65 | 62% | 64% | 61% | -176 |
| 2 mid | 633 | 70 | 62% | 67% | 57% | -150 |
| 3 high | 605 | 70 | 58% | 63% | 54% | 31 |

### FIRST HOUR: range % (tercile; entries from 10:20)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 533 | 72 | 65% | 66% | 63% | -365 |
| 2 mid | 512 | 55 | 58% | 63% | 54% | -69 |
| 3 high | 495 | 53 | 62% | 69% | 60% | -242 |

### FIRST HOUR: efficiency (tercile; entries from 10:20)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 528 | 61 | 65% | 64% | 66% | -340 |
| 2 mid | 508 | 61 | 60% | 65% | 56% | -182 |
| 3 high | 504 | 58 | 61% | 67% | 54% | -155 |

### KNOWN AT OPEN: opening gap
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| GapDown | 249 | 28 | 61% | 63% | 58% | -123 |
| GapUp | 462 | 50 | 60% | 64% | 58% | -115 |
| Normal | 1145 | 125 | 61% | 66% | 57% | -84 |
| unknown | 26 | 2 | 62% | 62% | - | -366 |

### Entry time (IST hour)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 09h | 212 | 126 | 56% | 64% | 51% | 577 |
| 10h | 320 | 138 | 61% | 60% | 63% | -96 |
| 11h | 279 | 119 | 63% | 67% | 59% | -190 |
| 12h | 304 | 127 | 63% | 68% | 58% | -266 |
| 13h | 309 | 127 | 61% | 66% | 56% | -135 |
| 14h | 370 | 150 | 65% | 68% | 63% | -244 |
| 15h | 88 | 74 | 35% | 46% | 24% | -170 |

## CALL|Base|LiveForReference | Call (n=229, pooled SL 61%)
Clustering: 14 session-symbols with >=4 trades; SD of daily SL share 0.113 vs 0.219 expected if independent; ratio 0.51 (>1 = clustered).
Worst sessions (>=6 trades): BANKNIFTY 10-26 Normal/Range 6/7 SL net -16644; BANKNIFTY 11-18 Normal/Range 5/6 SL net -10431; BANKNIFTY 09-29 GapDown/Range 5/7 SL net -7376; SENSEX 09-16 GapUp/Range 6/9 SL net -4417; BANKNIFTY 09-11 GapDown/Reversal 4/6 SL net -4007

### HINDSIGHT: day type (opening/path)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| Any/Unknown | 3 | 1 | 67% | - | - | 165 |
| GapDown/Continuation | 15 | 8 | 67% | 64% | - | -1019 |
| GapDown/Range | 14 | 4 | 71% | - | 71% | -558 |
| GapDown/Reversal | 26 | 9 | 54% | 57% | - | 320 |
| GapUp/Continuation | 5 | 3 | 80% | - | - | 568 |
| GapUp/Range | 20 | 8 | 50% | - | 53% | 334 |
| GapUp/Reversal | 6 | 5 | 17% | - | - | 2391 |
| Normal/Range | 69 | 26 | 65% | 67% | 64% | -480 |
| Normal/TrendDown | 50 | 22 | 68% | 68% | 68% | -654 |
| Normal/TrendUp | 21 | 11 | 48% | 56% | 42% | 1178 |

### HINDSIGHT: full-day range % (tercile within symbol)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 83 | 35 | 64% | 66% | 61% | -501 |
| 2 mid | 75 | 30 | 65% | 59% | 69% | -629 |
| 3 high | 71 | 32 | 54% | 60% | 49% | 806 |

### HINDSIGHT: full-day efficiency |close-open|/range (tercile)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 83 | 28 | 65% | 68% | 64% | -498 |
| 2 mid | 76 | 33 | 62% | 63% | 61% | -63 |
| 3 high | 70 | 36 | 56% | 60% | 48% | 209 |

### FIRST HOUR: range % (tercile; entries from 10:20)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 66 | 37 | 64% | 62% | 69% | -382 |
| 2 mid | 63 | 29 | 60% | 63% | 58% | -273 |
| 3 high | 62 | 23 | 60% | 60% | 60% | -325 |

### FIRST HOUR: efficiency (tercile; entries from 10:20)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 68 | 26 | 68% | 67% | 68% | -750 |
| 2 mid | 64 | 35 | 53% | 53% | 54% | 120 |
| 3 high | 59 | 28 | 63% | 70% | 55% | -326 |

### KNOWN AT OPEN: opening gap
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| GapDown | 55 | 21 | 62% | 59% | 67% | -269 |
| GapUp | 31 | 16 | 48% | 56% | 45% | 770 |
| Normal | 140 | 59 | 64% | 66% | 62% | -293 |
| unknown | 3 | 1 | 67% | - | - | 165 |

### Entry time (IST hour)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 09h | 25 | 20 | 52% | 55% | 50% | 1135 |
| 10h | 48 | 34 | 67% | 69% | 64% | -285 |
| 11h | 30 | 21 | 63% | 80% | 47% | -426 |
| 12h | 27 | 21 | 56% | 46% | 64% | -12 |
| 13h | 29 | 20 | 59% | 42% | 71% | 146 |
| 14h | 49 | 33 | 73% | 76% | 72% | -694 |
| 15h | 21 | 19 | 38% | 54% | 13% | -156 |

## PUT|Base|AllFiltersOff | Put (n=1979, pooled SL 59%)
Clustering: 173 session-symbols with >=4 trades; SD of daily SL share 0.161 vs 0.162 expected if independent; ratio 0.99 (>1 = clustered).
Worst sessions (>=6 trades): BANKNIFTY 11-09 GapUp/Continuation 7/8 SL net -6993; SENSEX 08-20 GapUp/Range 7/8 SL net -6765; BANKNIFTY 08-21 Normal/Range 7/8 SL net -4889; BANKNIFTY 11-12 Normal/TrendUp 6/7 SL net -12571; BANKNIFTY 08-12 Normal/TrendUp 6/7 SL net -5851; SENSEX 08-03 GapUp/Reversal 6/7 SL net -4790; BANKNIFTY 08-26 Normal/Range 5/6 SL net -7410; SENSEX 11-03 GapUp/Continuation 5/6 SL net -4257

### HINDSIGHT: day type (opening/path)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| Any/Unknown | 15 | 3 | 53% | 57% | - | -81 |
| GapDown/Continuation | 118 | 10 | 54% | 56% | 44% | 280 |
| GapDown/Range | 84 | 9 | 58% | 61% | 57% | 30 |
| GapDown/Reversal | 82 | 14 | 70% | 70% | 69% | -598 |
| GapUp/Continuation | 86 | 11 | 65% | 77% | 61% | -377 |
| GapUp/Range | 180 | 18 | 57% | 54% | 59% | 30 |
| GapUp/Reversal | 199 | 13 | 54% | 53% | 55% | 273 |
| Normal/Range | 421 | 46 | 62% | 61% | 64% | -274 |
| Normal/TrendDown | 545 | 44 | 56% | 55% | 56% | -19 |
| Normal/TrendUp | 249 | 35 | 61% | 66% | 58% | -270 |

### HINDSIGHT: full-day range % (tercile within symbol)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 687 | 80 | 61% | 60% | 63% | -225 |
| 2 mid | 663 | 63 | 59% | 58% | 60% | -113 |
| 3 high | 629 | 60 | 56% | 57% | 55% | 78 |

### HINDSIGHT: full-day efficiency |close-open|/range (tercile)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 672 | 68 | 60% | 59% | 62% | -158 |
| 2 mid | 665 | 69 | 57% | 57% | 58% | -25 |
| 3 high | 642 | 66 | 58% | 60% | 56% | -90 |

### FIRST HOUR: range % (tercile; entries from 10:20)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 563 | 65 | 57% | 59% | 48% | -61 |
| 2 mid | 552 | 64 | 61% | 63% | 60% | -197 |
| 3 high | 548 | 53 | 59% | 57% | 60% | -114 |

### FIRST HOUR: efficiency (tercile; entries from 10:20)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 570 | 63 | 62% | 63% | 60% | -190 |
| 2 mid | 552 | 59 | 57% | 57% | 56% | -106 |
| 3 high | 541 | 60 | 59% | 59% | 59% | -72 |

### KNOWN AT OPEN: opening gap
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| GapDown | 284 | 33 | 60% | 61% | 56% | -47 |
| GapUp | 465 | 42 | 57% | 56% | 58% | 59 |
| Normal | 1215 | 125 | 59% | 59% | 59% | -159 |
| unknown | 15 | 3 | 53% | 57% | - | -81 |

### Entry time (IST hour)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 09h | 222 | 122 | 57% | 54% | 61% | 104 |
| 10h | 315 | 128 | 58% | 54% | 61% | -99 |
| 11h | 362 | 137 | 61% | 62% | 60% | -63 |
| 12h | 316 | 141 | 60% | 63% | 58% | -83 |
| 13h | 390 | 139 | 61% | 65% | 57% | -64 |
| 14h | 294 | 129 | 59% | 58% | 59% | -206 |
| 15h | 80 | 69 | 38% | 32% | 42% | -471 |

## PUT|Base|LiveForReference | Put (n=330, pooled SL 59%)
Clustering: 40 session-symbols with >=4 trades; SD of daily SL share 0.262 vs 0.228 expected if independent; ratio 1.15 (>1 = clustered).
Worst sessions (>=6 trades): BANKNIFTY 08-10 Normal/TrendDown 6/6 SL net -9306; SENSEX 11-09 GapUp/Range 7/8 SL net -10021; BANKNIFTY 10-12 Normal/TrendDown 5/6 SL net -2328; BANKNIFTY 08-07 GapDown/Continuation 5/7 SL net -5159; SENSEX 10-19 Normal/TrendDown 7/11 SL net -4773; BANKNIFTY 09-08 Normal/TrendDown 3/6 SL net 569; SENSEX 11-12 GapUp/Range 3/6 SL net 1733; BANKNIFTY 07-22 Normal/TrendDown 3/6 SL net 3142

### HINDSIGHT: day type (opening/path)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| GapDown/Continuation | 31 | 6 | 45% | 45% | - | 168 |
| GapDown/Range | 17 | 7 | 59% | 54% | - | -131 |
| GapDown/Reversal | 13 | 7 | 69% | 69% | - | -673 |
| GapUp/Continuation | 18 | 7 | 56% | - | 56% | -397 |
| GapUp/Range | 51 | 15 | 47% | 27% | 62% | 243 |
| GapUp/Reversal | 23 | 8 | 61% | - | 53% | 2 |
| Normal/Range | 58 | 24 | 66% | 70% | 60% | -475 |
| Normal/TrendDown | 93 | 24 | 63% | 69% | 61% | -149 |
| Normal/TrendUp | 26 | 14 | 69% | 67% | 71% | -920 |

### HINDSIGHT: full-day range % (tercile within symbol)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 114 | 40 | 60% | 57% | 65% | -332 |
| 2 mid | 116 | 38 | 63% | 57% | 68% | -340 |
| 3 high | 100 | 34 | 55% | 63% | 51% | 114 |

### HINDSIGHT: full-day efficiency |close-open|/range (tercile)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 114 | 39 | 58% | 52% | 64% | -152 |
| 2 mid | 115 | 42 | 60% | 60% | 60% | -207 |
| 3 high | 101 | 31 | 60% | 64% | 58% | -245 |

### FIRST HOUR: range % (tercile; entries from 10:20)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 90 | 35 | 50% | 49% | 56% | 95 |
| 2 mid | 93 | 35 | 65% | 71% | 61% | -402 |
| 3 high | 81 | 27 | 57% | 56% | 57% | -18 |

### FIRST HOUR: efficiency (tercile; entries from 10:20)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 1 low | 97 | 36 | 56% | 56% | 55% | -204 |
| 2 mid | 84 | 28 | 58% | 56% | 60% | -143 |
| 3 high | 83 | 33 | 58% | 53% | 61% | 18 |

### KNOWN AT OPEN: opening gap
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| GapDown | 61 | 20 | 54% | 53% | - | -95 |
| GapUp | 92 | 30 | 52% | 39% | 58% | 57 |
| Normal | 177 | 62 | 65% | 69% | 62% | -369 |

### Entry time (IST hour)
| Group | n | sessions | SL% | H1 SL% | H2 SL% | Net/trade |
|---|---|---|---|---|---|---|
| 09h | 48 | 37 | 71% | 71% | 71% | -713 |
| 10h | 53 | 36 | 64% | 60% | 68% | -359 |
| 11h | 51 | 37 | 51% | 46% | 56% | 290 |
| 12h | 58 | 40 | 50% | 50% | 50% | 250 |
| 13h | 68 | 42 | 54% | 55% | 54% | -68 |
| 14h | 43 | 34 | 79% | 87% | 75% | -926 |
| 15h | 9 | 9 | 22% | - | - | 284 |

