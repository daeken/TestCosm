#!/bin/sh

clang test.c -c -o test.o --target=wasm32-wasi --sysroot=/opt/wasi-sdk/share/wasi-sysroot
wasm-ld --no-entry --export-dynamic --import-undefined -o test.wasm test.o /opt/wasi-sdk/share/wasi-sysroot/lib/wasm32-wasi/libc.a
cp test.wasm ../TestCosm/Assets/