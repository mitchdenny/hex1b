#!/usr/bin/env python3
"""A simple counter that outputs to stdout - used to test WithProcess
Exits after 20 seconds"""

import time

counter = 0
while counter < 20:
    print(f"Iteration {counter}", flush=True)
    counter += 1
    time.sleep(1)

print("Counter finished.")
