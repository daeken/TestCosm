debug("hi!")
debug(tostring(1234))
debug("Adding 5 and 6 with wasm: " .. tostring(add(5, 6)))

debug("Getting string from wasm: " .. retString())
