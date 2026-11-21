# Whipsaw shadow gate - historical replay

Score = same code as the live WhipsawShadowGate (3 binary signals, max 3). Closed bars before the entry minute only. Simulated, costed P&L (Rs20/order + 1pt); never TradeJournal P&L.
Policies remove trades from the simulated list; freed one-position slots are NOT re-simulated. 'Blocked net' > 0 means profit the policy would have removed. Outcome counts are TARGET/SL/NONE.

## CALL|Base|AllFiltersOff

Score distribution: score 0: 790, score 1: 868, score 2: 228, score 3: 2

### All (n=1888)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 1888 | 1888 | -191775 | 0.90 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 1888 | 1886 | -190799 | 0.90 | 2 | -976 | 1/1/0 |
| Block Caution+Severe (>=2) | 1888 | 1658 | -142007 | 0.92 | 230 | -49768 | 75/148/7 |

### Symbol BANKNIFTY (n=761)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 761 | 761 | -100902 | 0.89 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 761 | 761 | -100902 | 0.89 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 761 | 666 | -70004 | 0.92 | 95 | -30898 | 27/64/4 |

### Symbol NIFTY (n=113)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 113 | 113 | 7233 | 1.04 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 113 | 113 | 7233 | 1.04 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 113 | 101 | -8539 | 0.95 | 12 | 15772 | 7/5/0 |

### Symbol SENSEX (n=1014)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 1014 | 1014 | -98106 | 0.87 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 1014 | 1012 | -97130 | 0.88 | 2 | -976 | 1/1/0 |
| Block Caution+Severe (>=2) | 1014 | 891 | -63464 | 0.91 | 123 | -34642 | 41/79/3 |

### Side Call (n=1888)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 1888 | 1888 | -191775 | 0.90 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 1888 | 1886 | -190799 | 0.90 | 2 | -976 | 1/1/0 |
| Block Caution+Severe (>=2) | 1888 | 1658 | -142007 | 0.92 | 230 | -49768 | 75/148/7 |

### Opening GapUp (n=468)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 468 | 468 | -59126 | 0.88 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 468 | 468 | -59126 | 0.88 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 468 | 440 | -49534 | 0.89 | 28 | -9592 | 13/14/1 |

### Opening GapDown (n=249)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 249 | 249 | -30657 | 0.88 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 249 | 249 | -30657 | 0.88 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 249 | 202 | -24245 | 0.88 | 47 | -6411 | 15/31/1 |

### Opening Normal (n=1145)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 1145 | 1145 | -92469 | 0.92 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 1145 | 1143 | -91493 | 0.92 | 2 | -976 | 1/1/0 |
| Block Caution+Severe (>=2) | 1145 | 990 | -58705 | 0.94 | 155 | -33765 | 47/103/5 |

### First half of dates (<11-09-2026) (n=811)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 811 | 811 | -302851 | 0.62 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 811 | 809 | -301875 | 0.62 | 2 | -976 | 1/1/0 |
| Block Caution+Severe (>=2) | 811 | 678 | -249809 | 0.63 | 133 | -53042 | 43/88/2 |

### Second half of dates (>=11-09-2026) (n=1077)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 1077 | 1077 | 111076 | 1.10 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 1077 | 1077 | 111076 | 1.10 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 1077 | 980 | 107802 | 1.11 | 97 | 3274 | 32/60/5 |

### Which signal fires
1m EMA flips>=3: 13; 5m alternation>=3: 574; 1m/5m disagree: 743

## CALL|Base|LiveForReference

Score distribution: score 0: 3, score 1: 156, score 2: 69, score 3: 1

### All (n=229)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 229 | 229 | -30697 | 0.89 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 229 | 228 | -30498 | 0.89 | 1 | -200 | 0/1/0 |
| Block Caution+Severe (>=2) | 229 | 159 | -10821 | 0.94 | 70 | -19877 | 22/46/2 |

### Symbol BANKNIFTY (n=125)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 125 | 125 | -35538 | 0.81 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 125 | 125 | -35538 | 0.81 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 125 | 88 | -35961 | 0.73 | 37 | 423 | 15/20/2 |

### Symbol NIFTY (n=14)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 14 | 14 | 11593 | 1.68 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 14 | 14 | 11593 | 1.68 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 14 | 13 | 15196 | 2.13 | 1 | -3603 | 0/1/0 |

### Symbol SENSEX (n=90)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 90 | 90 | -6752 | 0.91 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 90 | 89 | -6553 | 0.91 | 1 | -200 | 0/1/0 |
| Block Caution+Severe (>=2) | 90 | 58 | 9944 | 1.22 | 32 | -16697 | 7/25/0 |

### Side Call (n=229)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 229 | 229 | -30697 | 0.89 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 229 | 228 | -30498 | 0.89 | 1 | -200 | 0/1/0 |
| Block Caution+Severe (>=2) | 229 | 159 | -10821 | 0.94 | 70 | -19877 | 22/46/2 |

### Opening GapUp (n=31)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 31 | 31 | 23857 | 2.08 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 31 | 31 | 23857 | 2.08 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 31 | 23 | 23518 | 2.50 | 8 | 339 | 3/5/0 |

### Opening GapDown (n=55)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 55 | 55 | -14769 | 0.76 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 55 | 55 | -14769 | 0.76 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 55 | 42 | -8591 | 0.83 | 13 | -6178 | 4/9/0 |

### Opening Normal (n=140)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 140 | 140 | -40279 | 0.79 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 140 | 139 | -40079 | 0.79 | 1 | -200 | 0/1/0 |
| Block Caution+Severe (>=2) | 140 | 91 | -26241 | 0.79 | 49 | -14038 | 15/32/2 |

### First half of dates (<10-09-2026) (n=97)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 97 | 97 | -24085 | 0.76 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 97 | 96 | -23885 | 0.76 | 1 | -200 | 0/1/0 |
| Block Caution+Severe (>=2) | 97 | 66 | -17306 | 0.76 | 31 | -6779 | 10/20/1 |

### Second half of dates (>=10-09-2026) (n=132)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 132 | 132 | -6613 | 0.96 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 132 | 132 | -6613 | 0.96 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 132 | 93 | 6485 | 1.05 | 39 | -13098 | 12/26/1 |

### Which signal fires
1m EMA flips>=3: 2; 5m alternation>=3: 69; 1m/5m disagree: 226

## PUT|Base|AllFiltersOff

Score distribution: score 0: 817, score 1: 906, score 2: 254

### All (n=1977)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 1977 | 1977 | -181077 | 0.90 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 1977 | 1977 | -181077 | 0.90 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 1977 | 1723 | -262585 | 0.84 | 254 | 81508 | 120/129/5 |

### Symbol BANKNIFTY (n=795)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 795 | 795 | -74336 | 0.92 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 795 | 795 | -74336 | 0.92 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 795 | 689 | -95663 | 0.88 | 106 | 21327 | 43/60/3 |

### Symbol NIFTY (n=114)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 114 | 114 | -37574 | 0.80 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 114 | 114 | -37574 | 0.80 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 114 | 100 | -44125 | 0.73 | 14 | 6551 | 7/7/0 |

### Symbol SENSEX (n=1068)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 1068 | 1068 | -69166 | 0.91 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 1068 | 1068 | -69166 | 0.91 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 1068 | 934 | -122797 | 0.82 | 134 | 53631 | 70/62/2 |

### Side Put (n=1977)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 1977 | 1977 | -181077 | 0.90 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 1977 | 1977 | -181077 | 0.90 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 1977 | 1723 | -262585 | 0.84 | 254 | 81508 | 120/129/5 |

### Opening GapUp (n=466)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 466 | 466 | 24710 | 1.06 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 466 | 466 | 24710 | 1.06 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 466 | 374 | -32995 | 0.90 | 92 | 57705 | 50/40/2 |

### Opening GapDown (n=284)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 284 | 284 | -13441 | 0.95 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 284 | 284 | -13441 | 0.95 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 284 | 271 | -25387 | 0.90 | 13 | 11946 | 7/6/0 |

### Opening Normal (n=1213)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 1213 | 1213 | -187302 | 0.84 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 1213 | 1213 | -187302 | 0.84 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 1213 | 1065 | -198250 | 0.81 | 148 | 10948 | 63/83/2 |

### First half of dates (<11-09-2026) (n=937)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 937 | 937 | -90965 | 0.88 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 937 | 937 | -90965 | 0.88 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 937 | 814 | -108328 | 0.83 | 123 | 17363 | 53/67/3 |

### Second half of dates (>=11-09-2026) (n=1040)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 1040 | 1040 | -90111 | 0.92 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 1040 | 1040 | -90111 | 0.92 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 1040 | 909 | -154257 | 0.84 | 131 | 64146 | 67/62/2 |

### Which signal fires
1m EMA flips>=3: 13; 5m alternation>=3: 606; 1m/5m disagree: 795

## PUT|Base|LiveForReference

Score distribution: score 0: 155, score 1: 144, score 2: 31

### All (n=330)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 330 | 330 | -65847 | 0.83 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 330 | 330 | -65847 | 0.83 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 330 | 299 | -85084 | 0.76 | 31 | 19237 | 18/12/1 |

### Symbol BANKNIFTY (n=212)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 212 | 212 | -48039 | 0.81 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 212 | 212 | -48039 | 0.81 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 212 | 196 | -66539 | 0.72 | 16 | 18500 | 11/5/0 |

### Symbol NIFTY (n=28)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 28 | 28 | -23144 | 0.56 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 28 | 28 | -23144 | 0.56 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 28 | 25 | -22872 | 0.51 | 3 | -272 | 1/2/0 |

### Symbol SENSEX (n=90)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 90 | 90 | 5336 | 1.07 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 90 | 90 | 5336 | 1.07 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 90 | 78 | 4328 | 1.07 | 12 | 1008 | 6/5/1 |

### Side Put (n=330)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 330 | 330 | -65847 | 0.83 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 330 | 330 | -65847 | 0.83 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 330 | 299 | -85084 | 0.76 | 31 | 19237 | 18/12/1 |

### Opening GapUp (n=92)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 92 | 92 | 5274 | 1.05 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 92 | 92 | 5274 | 1.05 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 92 | 69 | -10322 | 0.87 | 23 | 15597 | 14/8/1 |

### Opening GapDown (n=61)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 61 | 61 | -5789 | 0.91 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 61 | 61 | -5789 | 0.91 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 61 | 61 | -5789 | 0.91 | 0 | 0 | 0/0/0 |

### Opening Normal (n=177)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 177 | 177 | -65332 | 0.70 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 177 | 177 | -65332 | 0.70 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 177 | 169 | -68972 | 0.67 | 8 | 3640 | 4/4/0 |

### First half of dates (<11-09-2026) (n=135)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 135 | 135 | -25553 | 0.82 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 135 | 135 | -25553 | 0.82 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 135 | 125 | -36387 | 0.73 | 10 | 10834 | 7/2/1 |

### Second half of dates (>=11-09-2026) (n=195)
| Policy | Trades | Kept | Kept net | Kept PF | Blocked | Blocked net | Blocked T/SL/NONE |
|---|---|---|---|---|---|---|---|
| Never block (shadow) | 195 | 195 | -40293 | 0.83 | 0 | 0 | 0/0/0 |
| Block Severe (>=3) | 195 | 195 | -40293 | 0.83 | 0 | 0 | 0/0/0 |
| Block Caution+Severe (>=2) | 195 | 174 | -48696 | 0.78 | 21 | 8403 | 11/10/0 |

### Which signal fires
1m EMA flips>=3: 0; 5m alternation>=3: 99; 1m/5m disagree: 107

