#include <stdlib.h>
#include <string.h>
#include <ctype.h>

extern void debug(char*);

__attribute__((__visibility__("default"))) int add(int a, int b) {
	return a + b;
}

__attribute__((__visibility__("default"))) void callDebug() {
	debug("Hello WASM world!");
}

__attribute__((__visibility__("default"))) char* retString() {
	return "Hi from WASM!";
}

__attribute__((__visibility__("default"))) char* makeUpper(char* str) {
	int len = strlen(str);
	char* ret = (char*) malloc(len + 1);
	for(int i = 0; i < len; ++i)
		ret[i] = toupper(str[i]);
	ret[len] = 0;
	return ret;
}
