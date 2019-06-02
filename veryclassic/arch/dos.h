#include <stdint.h>
#pragma GCC diagnostic push
#pragma GCC diagnostic ignored "-Wunused-variable"
static intptr_t find_last;
#pragma GCC diagnostic pop
#define _dos_findfirst(spec, x, data) ((find_last = _findfirst(spec, data)) == -1)
#define _dos_findnext(data) _findnext(find_last, data)
#define find_t _finddata_t
