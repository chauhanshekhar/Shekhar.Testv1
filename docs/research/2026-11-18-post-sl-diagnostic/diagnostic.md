# Post-stop-loss trade diagnostic

Simulated, costed (Rs20/order + 1pt) entries of the live rules and of the unfiltered base rate. Features use closed bars before the entry minute; reversal is on the underlying, in 1m-ATR units.
Cut-offs: Low EMA gap = bottom third of |EMA20-EMA50|/LTP per symbol; Large distance = top third of side-adjusted (LTP-EMA20)/EMA20 per symbol (positive = extended in trade direction); High reversals = top third of 1m direction changes in the previous 15 bars per symbol. Descriptive, not tradable thresholds.
SL% = share of the group's trades that are SL. Compare each group with 'All' in the same table; H1/H2 = SL% in the first/second half of the dates.

## CALL|Base|AllFiltersOff | Call | ALL (n=1888)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 1888 | 694 | 1150 | 44 | 61% | 65% | 57% | -195627 | -104 |
| Previous trade was SL | 1148 | 420 | 702 | 26 | 61% | 65% | 57% | -112371 | -98 |
|   ...within 15 min of that SL | 738 | 259 | 458 | 21 | 62% | 66% | 59% | -120268 | -163 |
|   ...16-60 min after that SL | 188 | 77 | 108 | 3 | 57% | 57% | 58% | -19118 | -102 |
| Previous trade was TARGET | 693 | 254 | 422 | 17 | 61% | 64% | 58% | -122452 | -177 |
| Low EMA gap (bottom third) | 631 | 233 | 384 | 14 | 61% | 64% | 56% | -148054 | -235 |
| Large distance from EMA20 (top third) | 630 | 236 | 379 | 15 | 60% | 62% | 59% | 16112 | 26 |
| High 1m reversals (top third) | 669 | 247 | 403 | 19 | 60% | 64% | 56% | -58760 | -88 |
| 5m reversals >= 3 of last 5 | 574 | 213 | 350 | 11 | 61% | 66% | 55% | -41723 | -73 |
| Prev direction reversed >= 0.5 ATR | 671 | 253 | 403 | 15 | 60% | 64% | 55% | -58729 | -88 |
| Prev direction reversed >= 1 ATR | 485 | 184 | 292 | 9 | 60% | 66% | 53% | -58322 | -120 |
| Prev direction reversed >= 2 ATR | 265 | 104 | 156 | 5 | 59% | 68% | 48% | -12333 | -47 |
| Prev max adverse >= 1 ATR | 1359 | 504 | 826 | 29 | 61% | 64% | 57% | -147449 | -108 |
| Prev max favourable >= 1 ATR | 1244 | 457 | 757 | 30 | 61% | 64% | 58% | -132095 | -106 |

## CALL|Base|AllFiltersOff | Call | BANKNIFTY (n=761)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 761 | 264 | 475 | 22 | 62% | 66% | 59% | -102982 | -135 |
| Previous trade was SL | 474 | 160 | 301 | 13 | 64% | 67% | 60% | -82242 | -174 |
|   ...within 15 min of that SL | 300 | 100 | 189 | 11 | 63% | 66% | 60% | -47083 | -157 |
|   ...16-60 min after that SL | 89 | 27 | 60 | 2 | 67% | 66% | 69% | -38037 | -427 |
| Previous trade was TARGET | 264 | 93 | 163 | 8 | 62% | 65% | 59% | -61037 | -231 |
| Low EMA gap (bottom third) | 254 | 78 | 171 | 5 | 67% | 72% | 59% | -112571 | -443 |
| Large distance from EMA20 (top third) | 254 | 85 | 163 | 6 | 64% | 60% | 67% | -47954 | -189 |
| High 1m reversals (top third) | 267 | 96 | 161 | 10 | 60% | 63% | 57% | 7421 | 28 |
| 5m reversals >= 3 of last 5 | 213 | 74 | 133 | 6 | 62% | 67% | 58% | -19035 | -89 |
| Prev direction reversed >= 0.5 ATR | 284 | 99 | 177 | 8 | 62% | 66% | 58% | -31280 | -110 |
| Prev direction reversed >= 1 ATR | 207 | 72 | 131 | 4 | 63% | 67% | 58% | -37375 | -181 |
| Prev direction reversed >= 2 ATR | 111 | 41 | 67 | 3 | 60% | 64% | 56% | -19907 | -179 |
| Prev max adverse >= 1 ATR | 565 | 192 | 359 | 14 | 64% | 66% | 61% | -104361 | -185 |
| Prev max favourable >= 1 ATR | 488 | 171 | 300 | 17 | 61% | 65% | 59% | -71600 | -147 |

## CALL|Base|AllFiltersOff | Call | NIFTY (n=113)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 113 | 50 | 62 | 1 | 55% | 64% | 54% | 7233 | 64 |
| Previous trade was SL | 62 | 33 | 29 | 0 | 47% | 63% | 44% | 49651 | 801 |
|   ...within 15 min of that SL | 29 | 14 | 15 | 0 | 52% | - | 54% | -4258 | -147 |
|   ...16-60 min after that SL | 2 | 1 | 1 | 0 | 50% | - | - | 3731 | 1865 |
| Previous trade was TARGET | 49 | 17 | 31 | 1 | 63% | 60% | 64% | -36336 | -742 |
| Low EMA gap (bottom third) | 38 | 19 | 19 | 0 | 50% | 57% | 48% | 22180 | 584 |
| Large distance from EMA20 (top third) | 38 | 20 | 18 | 0 | 47% | 100% | 39% | 44561 | 1173 |
| High 1m reversals (top third) | 52 | 24 | 28 | 0 | 54% | 71% | 51% | -493 | -9 |
| 5m reversals >= 3 of last 5 | 30 | 17 | 13 | 0 | 43% | - | 41% | 24836 | 828 |
| Prev direction reversed >= 0.5 ATR | 39 | 19 | 20 | 0 | 51% | 83% | 45% | 19434 | 498 |
| Prev direction reversed >= 1 ATR | 30 | 14 | 16 | 0 | 53% | 83% | 46% | 8239 | 275 |
| Prev direction reversed >= 2 ATR | 19 | 13 | 6 | 0 | 32% | 80% | 14% | 31762 | 1672 |
| Prev max adverse >= 1 ATR | 78 | 34 | 44 | 0 | 56% | 67% | 55% | 8349 | 107 |
| Prev max favourable >= 1 ATR | 87 | 35 | 51 | 1 | 59% | 67% | 57% | -2715 | -31 |

## CALL|Base|AllFiltersOff | Call | SENSEX (n=1014)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 1014 | 380 | 613 | 21 | 60% | 64% | 56% | -99878 | -98 |
| Previous trade was SL | 612 | 227 | 372 | 13 | 61% | 64% | 56% | -79779 | -130 |
|   ...within 15 min of that SL | 409 | 145 | 254 | 10 | 62% | 66% | 58% | -68927 | -169 |
|   ...16-60 min after that SL | 97 | 49 | 47 | 1 | 48% | 49% | 46% | 15188 | 157 |
| Previous trade was TARGET | 380 | 144 | 228 | 8 | 60% | 64% | 56% | -25079 | -66 |
| Low EMA gap (bottom third) | 339 | 136 | 194 | 9 | 57% | 58% | 55% | -57663 | -170 |
| Large distance from EMA20 (top third) | 338 | 131 | 198 | 9 | 59% | 61% | 57% | 19504 | 58 |
| High 1m reversals (top third) | 350 | 127 | 214 | 9 | 61% | 65% | 56% | -65688 | -188 |
| 5m reversals >= 3 of last 5 | 331 | 122 | 204 | 5 | 62% | 66% | 56% | -47524 | -144 |
| Prev direction reversed >= 0.5 ATR | 348 | 135 | 206 | 7 | 59% | 63% | 54% | -46884 | -135 |
| Prev direction reversed >= 1 ATR | 248 | 98 | 145 | 5 | 58% | 65% | 51% | -29185 | -118 |
| Prev direction reversed >= 2 ATR | 135 | 50 | 83 | 2 | 61% | 71% | 49% | -24188 | -179 |
| Prev max adverse >= 1 ATR | 716 | 278 | 423 | 15 | 59% | 63% | 54% | -51437 | -72 |
| Prev max favourable >= 1 ATR | 669 | 251 | 406 | 12 | 61% | 64% | 57% | -57780 | -86 |

## CALL|Base|LiveForReference | Call | ALL (n=229)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 229 | 80 | 140 | 9 | 61% | 63% | 60% | -31450 | -137 |
| Previous trade was SL | 138 | 49 | 83 | 6 | 60% | 63% | 58% | -15146 | -110 |
|   ...within 15 min of that SL | 46 | 14 | 29 | 3 | 63% | 61% | 64% | -13332 | -290 |
|   ...16-60 min after that SL | 23 | 7 | 16 | 0 | 70% | 75% | 64% | -12349 | -537 |
| Previous trade was TARGET | 79 | 24 | 53 | 2 | 67% | 68% | 67% | -30036 | -380 |
| Low EMA gap (bottom third) | 78 | 29 | 47 | 2 | 60% | 58% | 63% | -24378 | -313 |
| Large distance from EMA20 (top third) | 77 | 28 | 48 | 1 | 62% | 61% | 64% | -9877 | -128 |
| High 1m reversals (top third) | 84 | 31 | 50 | 3 | 60% | 62% | 57% | -18131 | -216 |
| 5m reversals >= 3 of last 5 | 69 | 22 | 46 | 1 | 67% | 67% | 67% | -19108 | -277 |
| Prev direction reversed >= 0.5 ATR | 95 | 35 | 55 | 5 | 58% | 56% | 60% | -13015 | -137 |
| Prev direction reversed >= 1 ATR | 83 | 31 | 48 | 4 | 58% | 59% | 56% | -10009 | -121 |
| Prev direction reversed >= 2 ATR | 66 | 27 | 35 | 4 | 53% | 54% | 52% | 3343 | 51 |
| Prev max adverse >= 1 ATR | 195 | 70 | 117 | 8 | 60% | 59% | 61% | 3318 | 17 |
| Prev max favourable >= 1 ATR | 180 | 62 | 110 | 8 | 61% | 64% | 58% | -20103 | -112 |

## CALL|Base|LiveForReference | Call | BANKNIFTY (n=125)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 125 | 44 | 76 | 5 | 61% | 61% | 60% | -36291 | -290 |
| Previous trade was SL | 75 | 30 | 42 | 3 | 56% | 59% | 53% | -7552 | -101 |
|   ...within 15 min of that SL | 23 | 8 | 13 | 2 | 57% | 44% | 64% | -9667 | -420 |
|   ...16-60 min after that SL | 17 | 6 | 11 | 0 | 65% | 73% | 50% | -7870 | -463 |
| Previous trade was TARGET | 44 | 11 | 32 | 1 | 73% | 73% | 73% | -30178 | -686 |
| Low EMA gap (bottom third) | 42 | 14 | 27 | 1 | 64% | 59% | 73% | -25060 | -597 |
| Large distance from EMA20 (top third) | 42 | 13 | 28 | 1 | 67% | 65% | 68% | -22830 | -544 |
| High 1m reversals (top third) | 43 | 15 | 26 | 2 | 60% | 59% | 63% | -20398 | -474 |
| 5m reversals >= 3 of last 5 | 37 | 15 | 21 | 1 | 57% | 64% | 47% | -337 | -9 |
| Prev direction reversed >= 0.5 ATR | 49 | 22 | 25 | 2 | 51% | 54% | 48% | -4607 | -94 |
| Prev direction reversed >= 1 ATR | 43 | 18 | 23 | 2 | 53% | 60% | 44% | -8750 | -203 |
| Prev direction reversed >= 2 ATR | 36 | 16 | 18 | 2 | 50% | 55% | 43% | -744 | -21 |
| Prev max adverse >= 1 ATR | 106 | 41 | 60 | 5 | 57% | 57% | 56% | -2660 | -25 |
| Prev max favourable >= 1 ATR | 93 | 31 | 58 | 4 | 62% | 64% | 60% | -24833 | -267 |

## CALL|Base|LiveForReference | Call | SENSEX (n=90)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 90 | 28 | 59 | 3 | 66% | 65% | 66% | -6752 | -75 |
| Previous trade was SL | 59 | 16 | 40 | 3 | 68% | 69% | 67% | -11362 | -193 |
|   ...within 15 min of that SL | 21 | 4 | 16 | 1 | 76% | 78% | 75% | -9384 | -447 |
|   ...16-60 min after that SL | 6 | 1 | 5 | 0 | 83% | - | 80% | -4479 | -746 |
| Previous trade was TARGET | 27 | 9 | 18 | 0 | 67% | 58% | 73% | -6233 | -231 |
| Low EMA gap (bottom third) | 31 | 12 | 18 | 1 | 58% | 56% | 60% | 331 | 11 |
| Large distance from EMA20 (top third) | 30 | 11 | 19 | 0 | 63% | 50% | 70% | -45 | -2 |
| High 1m reversals (top third) | 34 | 11 | 23 | 0 | 68% | 67% | 68% | -8979 | -264 |
| 5m reversals >= 3 of last 5 | 31 | 7 | 24 | 0 | 77% | 71% | 82% | -15169 | -489 |
| Prev direction reversed >= 0.5 ATR | 42 | 11 | 28 | 3 | 67% | 60% | 73% | -6423 | -153 |
| Prev direction reversed >= 1 ATR | 36 | 11 | 23 | 2 | 64% | 58% | 71% | 726 | 20 |
| Prev direction reversed >= 2 ATR | 27 | 10 | 15 | 2 | 56% | 53% | 60% | 9375 | 347 |
| Prev max adverse >= 1 ATR | 80 | 23 | 54 | 3 | 68% | 62% | 72% | -10259 | -128 |
| Prev max favourable >= 1 ATR | 75 | 25 | 47 | 3 | 63% | 65% | 61% | -1550 | -21 |

## PUT|Base|AllFiltersOff | Put | ALL (n=1980)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 1980 | 783 | 1159 | 38 | 59% | 59% | 58% | -183064 | -92 |
| Previous trade was SL | 1156 | 452 | 675 | 29 | 58% | 57% | 59% | -102435 | -89 |
|   ...within 15 min of that SL | 747 | 295 | 436 | 16 | 58% | 56% | 61% | -103819 | -139 |
|   ...16-60 min after that SL | 207 | 73 | 124 | 10 | 60% | 61% | 59% | -16298 | -79 |
| Previous trade was TARGET | 783 | 313 | 461 | 9 | 59% | 61% | 57% | -86529 | -111 |
| Low EMA gap (bottom third) | 662 | 258 | 384 | 20 | 58% | 58% | 58% | -34800 | -53 |
| Large distance from EMA20 (top third) | 661 | 264 | 386 | 11 | 58% | 59% | 58% | -25867 | -39 |
| High 1m reversals (top third) | 844 | 320 | 507 | 17 | 60% | 63% | 57% | -97531 | -116 |
| 5m reversals >= 3 of last 5 | 608 | 262 | 334 | 12 | 55% | 56% | 53% | 28463 | 47 |
| Prev direction reversed >= 0.5 ATR | 638 | 244 | 383 | 11 | 60% | 59% | 61% | -76981 | -121 |
| Prev direction reversed >= 1 ATR | 474 | 178 | 288 | 8 | 61% | 61% | 61% | -53210 | -112 |
| Prev direction reversed >= 2 ATR | 277 | 104 | 167 | 6 | 60% | 62% | 59% | -25474 | -92 |
| Prev max adverse >= 1 ATR | 1380 | 527 | 823 | 30 | 60% | 58% | 61% | -186881 | -135 |
| Prev max favourable >= 1 ATR | 1368 | 553 | 789 | 26 | 58% | 58% | 57% | -85685 | -63 |

## PUT|Base|AllFiltersOff | Put | BANKNIFTY (n=796)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 796 | 308 | 470 | 18 | 59% | 60% | 58% | -74564 | -94 |
| Previous trade was SL | 469 | 179 | 276 | 14 | 59% | 59% | 59% | -35825 | -76 |
|   ...within 15 min of that SL | 293 | 114 | 171 | 8 | 58% | 59% | 58% | -27276 | -93 |
|   ...16-60 min after that SL | 100 | 41 | 55 | 4 | 55% | 52% | 60% | 6360 | 64 |
| Previous trade was TARGET | 308 | 121 | 183 | 4 | 59% | 63% | 56% | -41928 | -136 |
| Low EMA gap (bottom third) | 266 | 102 | 154 | 10 | 58% | 56% | 60% | -3212 | -12 |
| Large distance from EMA20 (top third) | 266 | 109 | 153 | 4 | 58% | 58% | 57% | -9583 | -36 |
| High 1m reversals (top third) | 399 | 147 | 243 | 9 | 61% | 63% | 59% | -62374 | -156 |
| 5m reversals >= 3 of last 5 | 243 | 93 | 144 | 6 | 59% | 61% | 57% | -5197 | -21 |
| Prev direction reversed >= 0.5 ATR | 258 | 91 | 161 | 6 | 62% | 62% | 63% | -56391 | -219 |
| Prev direction reversed >= 1 ATR | 189 | 71 | 115 | 3 | 61% | 63% | 59% | -30542 | -162 |
| Prev direction reversed >= 2 ATR | 108 | 37 | 68 | 3 | 63% | 67% | 59% | -27569 | -255 |
| Prev max adverse >= 1 ATR | 553 | 207 | 330 | 16 | 60% | 60% | 60% | -52737 | -95 |
| Prev max favourable >= 1 ATR | 551 | 220 | 320 | 11 | 58% | 59% | 57% | -35231 | -64 |

## PUT|Base|AllFiltersOff | Put | NIFTY (n=114)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 114 | 49 | 65 | 0 | 57% | 57% | 57% | -37574 | -330 |
| Previous trade was SL | 64 | 26 | 38 | 0 | 59% | 43% | 61% | -32352 | -505 |
|   ...within 15 min of that SL | 30 | 9 | 21 | 0 | 70% | - | 72% | -34447 | -1148 |
|   ...16-60 min after that SL | 4 | 2 | 2 | 0 | 50% | - | - | 3571 | 893 |
| Previous trade was TARGET | 49 | 23 | 26 | 0 | 53% | 67% | 51% | -2424 | -49 |
| Low EMA gap (bottom third) | 39 | 19 | 20 | 0 | 51% | - | 50% | 10543 | 270 |
| Large distance from EMA20 (top third) | 38 | 15 | 23 | 0 | 61% | 75% | 57% | -25507 | -671 |
| High 1m reversals (top third) | 56 | 27 | 29 | 0 | 52% | 50% | 52% | 4456 | 80 |
| 5m reversals >= 3 of last 5 | 26 | 11 | 15 | 0 | 58% | - | 57% | -749 | -29 |
| Prev direction reversed >= 0.5 ATR | 44 | 21 | 23 | 0 | 52% | - | 55% | -1550 | -35 |
| Prev direction reversed >= 1 ATR | 39 | 19 | 20 | 0 | 51% | - | 54% | 6789 | 174 |
| Prev direction reversed >= 2 ATR | 28 | 14 | 14 | 0 | 50% | - | 54% | 5084 | 182 |
| Prev max adverse >= 1 ATR | 90 | 37 | 53 | 0 | 59% | 50% | 60% | -33366 | -371 |
| Prev max favourable >= 1 ATR | 81 | 35 | 46 | 0 | 57% | 64% | 56% | -20722 | -256 |

## PUT|Base|AllFiltersOff | Put | SENSEX (n=1070)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 1070 | 426 | 624 | 20 | 58% | 58% | 59% | -70926 | -66 |
| Previous trade was SL | 623 | 247 | 361 | 15 | 58% | 57% | 59% | -34258 | -55 |
|   ...within 15 min of that SL | 424 | 172 | 244 | 8 | 58% | 54% | 61% | -42096 | -99 |
|   ...16-60 min after that SL | 103 | 30 | 67 | 6 | 65% | 69% | 59% | -26228 | -255 |
| Previous trade was TARGET | 426 | 169 | 252 | 5 | 59% | 59% | 59% | -42178 | -99 |
| Low EMA gap (bottom third) | 357 | 137 | 210 | 10 | 59% | 59% | 58% | -42131 | -118 |
| Large distance from EMA20 (top third) | 357 | 140 | 210 | 7 | 59% | 58% | 60% | 9223 | 26 |
| High 1m reversals (top third) | 389 | 146 | 235 | 8 | 60% | 64% | 57% | -39613 | -102 |
| 5m reversals >= 3 of last 5 | 339 | 158 | 175 | 6 | 52% | 53% | 50% | 34409 | 102 |
| Prev direction reversed >= 0.5 ATR | 336 | 132 | 199 | 5 | 59% | 58% | 61% | -19040 | -57 |
| Prev direction reversed >= 1 ATR | 246 | 88 | 153 | 5 | 62% | 60% | 64% | -29458 | -120 |
| Prev direction reversed >= 2 ATR | 141 | 53 | 85 | 3 | 60% | 60% | 61% | -2990 | -21 |
| Prev max adverse >= 1 ATR | 737 | 283 | 440 | 14 | 60% | 57% | 63% | -100778 | -137 |
| Prev max favourable >= 1 ATR | 736 | 298 | 423 | 15 | 57% | 57% | 58% | -29732 | -40 |

## PUT|Base|LiveForReference | Put | ALL (n=330)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 330 | 129 | 196 | 5 | 59% | 58% | 61% | -65836 | -200 |
| Previous trade was SL | 195 | 74 | 118 | 3 | 61% | 60% | 61% | -51571 | -264 |
|   ...within 15 min of that SL | 54 | 22 | 32 | 0 | 59% | 62% | 58% | -12270 | -227 |
|   ...16-60 min after that SL | 41 | 15 | 24 | 2 | 59% | 55% | 62% | 4215 | 103 |
| Previous trade was TARGET | 127 | 50 | 75 | 2 | 59% | 58% | 60% | -29421 | -232 |
| Low EMA gap (bottom third) | 112 | 46 | 62 | 4 | 55% | 46% | 63% | -19378 | -173 |
| Large distance from EMA20 (top third) | 111 | 42 | 67 | 2 | 60% | 52% | 65% | -32575 | -293 |
| High 1m reversals (top third) | 124 | 52 | 68 | 4 | 55% | 54% | 56% | -1889 | -15 |
| 5m reversals >= 3 of last 5 | 99 | 41 | 56 | 2 | 57% | 45% | 65% | -5649 | -57 |
| Prev direction reversed >= 0.5 ATR | 84 | 34 | 49 | 1 | 58% | 48% | 64% | -23724 | -282 |
| Prev direction reversed >= 1 ATR | 75 | 30 | 44 | 1 | 59% | 44% | 66% | -27752 | -370 |
| Prev direction reversed >= 2 ATR | 66 | 28 | 37 | 1 | 56% | 38% | 64% | -15857 | -240 |
| Prev max adverse >= 1 ATR | 273 | 110 | 158 | 5 | 58% | 55% | 60% | -51686 | -189 |
| Prev max favourable >= 1 ATR | 282 | 110 | 168 | 4 | 60% | 59% | 60% | -52280 | -185 |

## PUT|Base|LiveForReference | Put | BANKNIFTY (n=212)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 212 | 81 | 127 | 4 | 60% | 59% | 61% | -48028 | -227 |
| Previous trade was SL | 127 | 50 | 74 | 3 | 58% | 59% | 58% | -24116 | -190 |
|   ...within 15 min of that SL | 30 | 15 | 15 | 0 | 50% | 61% | 33% | 7326 | 244 |
|   ...16-60 min after that SL | 31 | 10 | 19 | 2 | 61% | 61% | 62% | -4628 | -149 |
| Previous trade was TARGET | 80 | 28 | 51 | 1 | 64% | 61% | 69% | -34380 | -430 |
| Low EMA gap (bottom third) | 71 | 28 | 40 | 3 | 56% | 48% | 69% | -12912 | -182 |
| Large distance from EMA20 (top third) | 71 | 28 | 41 | 2 | 58% | 54% | 60% | -14843 | -209 |
| High 1m reversals (top third) | 72 | 26 | 43 | 3 | 60% | 58% | 63% | -12885 | -179 |
| 5m reversals >= 3 of last 5 | 58 | 26 | 31 | 1 | 53% | 43% | 70% | 2890 | 50 |
| Prev direction reversed >= 0.5 ATR | 55 | 21 | 33 | 1 | 60% | 52% | 67% | -14870 | -270 |
| Prev direction reversed >= 1 ATR | 49 | 19 | 29 | 1 | 59% | 48% | 68% | -14343 | -293 |
| Prev direction reversed >= 2 ATR | 42 | 17 | 24 | 1 | 57% | 41% | 68% | -8838 | -210 |
| Prev max adverse >= 1 ATR | 178 | 70 | 104 | 4 | 58% | 56% | 62% | -37004 | -208 |
| Prev max favourable >= 1 ATR | 176 | 66 | 107 | 3 | 61% | 60% | 62% | -40721 | -231 |

## PUT|Base|LiveForReference | Put | NIFTY (n=28)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 28 | 9 | 19 | 0 | 68% | 73% | 65% | -23144 | -827 |
| Previous trade was SL | 18 | 3 | 15 | 0 | 83% | 100% | 73% | -30527 | -1696 |
|   ...within 15 min of that SL | 5 | 0 | 5 | 0 | 100% | - | - | -14494 | -2899 |
|   ...16-60 min after that SL | 2 | 1 | 1 | 0 | 50% | - | - | 4766 | 2383 |
| Previous trade was TARGET | 9 | 5 | 4 | 0 | 44% | - | 50% | 3836 | 426 |
| Low EMA gap (bottom third) | 10 | 3 | 7 | 0 | 70% | - | 75% | -12162 | -1216 |
| Large distance from EMA20 (top third) | 10 | 2 | 8 | 0 | 80% | 80% | 80% | -17067 | -1707 |
| High 1m reversals (top third) | 11 | 4 | 7 | 0 | 64% | 60% | 67% | -6038 | -549 |
| 5m reversals >= 3 of last 5 | 8 | 1 | 7 | 0 | 88% | - | 80% | -10375 | -1297 |
| Prev direction reversed >= 0.5 ATR | 9 | 3 | 6 | 0 | 67% | - | 71% | -10182 | -1131 |
| Prev direction reversed >= 1 ATR | 8 | 2 | 6 | 0 | 75% | - | 83% | -14003 | -1750 |
| Prev direction reversed >= 2 ATR | 7 | 2 | 5 | 0 | 71% | - | 80% | -11017 | -1574 |
| Prev max adverse >= 1 ATR | 25 | 6 | 19 | 0 | 76% | 80% | 73% | -34997 | -1400 |
| Prev max favourable >= 1 ATR | 23 | 7 | 16 | 0 | 70% | 80% | 62% | -21086 | -917 |

## PUT|Base|LiveForReference | Put | SENSEX (n=90)
| Group | n | TARGET | SL | NONE | SL% | H1 SL% | H2 SL% | Net | Net/trade |
|---|---|---|---|---|---|---|---|---|---|
| All | 90 | 39 | 50 | 1 | 56% | 42% | 59% | 5336 | 59 |
| Previous trade was SL | 50 | 21 | 29 | 0 | 58% | 29% | 63% | 3072 | 61 |
|   ...within 15 min of that SL | 19 | 7 | 12 | 0 | 63% | - | 67% | -5102 | -269 |
|   ...16-60 min after that SL | 8 | 4 | 4 | 0 | 50% | - | 67% | 4077 | 510 |
| Previous trade was TARGET | 38 | 17 | 20 | 1 | 53% | 50% | 54% | 1123 | 30 |
| Low EMA gap (bottom third) | 31 | 15 | 15 | 1 | 48% | 38% | 52% | 5697 | 184 |
| Large distance from EMA20 (top third) | 30 | 12 | 18 | 0 | 60% | 33% | 71% | -664 | -22 |
| High 1m reversals (top third) | 41 | 22 | 18 | 1 | 44% | 17% | 49% | 17034 | 415 |
| 5m reversals >= 3 of last 5 | 33 | 14 | 18 | 1 | 55% | 33% | 59% | 1836 | 56 |
| Prev direction reversed >= 0.5 ATR | 20 | 10 | 10 | 0 | 50% | - | 56% | 1327 | 66 |
| Prev direction reversed >= 1 ATR | 18 | 9 | 9 | 0 | 50% | - | 56% | 594 | 33 |
| Prev direction reversed >= 2 ATR | 17 | 9 | 8 | 0 | 47% | - | 53% | 3998 | 235 |
| Prev max adverse >= 1 ATR | 70 | 34 | 35 | 1 | 50% | 33% | 55% | 20315 | 290 |
| Prev max favourable >= 1 ATR | 83 | 37 | 45 | 1 | 54% | 39% | 58% | 9528 | 115 |

