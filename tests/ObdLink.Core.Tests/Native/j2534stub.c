/* Minimal J2534 v04.04 stub for NativeJ2534Api tests (built with gcc on Linux by the test).
   Uses uint32_t for every "unsigned long" so the layout matches the Windows ABI the app ships for.
   Behaviour: one device, one channel; a written ISO15765 message is answered with a copy whose CAN ID is +8 and whose
   first payload byte is +0x40 (a positive response); SET_CONFIG values and the last flow-control filter are readable
   through Stub_* exports. Protocol 99 fails with ERR_INVALID_PROTOCOL_ID and a GetLastError text. */
#include <stdint.h>
#include <string.h>

typedef struct { uint32_t ProtocolID, RxStatus, TxFlags, Timestamp, DataSize, ExtraDataIndex; unsigned char Data[4128]; } PASSTHRU_MSG;
typedef struct { uint32_t Parameter, Value; } SCONFIG;
typedef struct { uint32_t NumOfParams; SCONFIG *ConfigPtr; } SCONFIG_LIST;

static PASSTHRU_MSG queue[16]; static int qn = 0;
static uint32_t cfg_param[16], cfg_value[16]; static int cn = 0;
static uint32_t fc_mask, fc_pattern, fc_flow, filter_type;
static int open_ = 0;
static char lasterr[80] = "";

static uint32_t id_of(const PASSTHRU_MSG *m) { return (uint32_t)m->Data[0] << 24 | (uint32_t)m->Data[1] << 16 | (uint32_t)m->Data[2] << 8 | m->Data[3]; }

int32_t PassThruOpen(void *name, uint32_t *dev) { open_ = 1; *dev = 42; return 0; }
int32_t PassThruClose(uint32_t dev) { open_ = 0; return dev == 42 ? 0 : 0x1A; }
int32_t PassThruConnect(uint32_t dev, uint32_t proto, uint32_t flags, uint32_t baud, uint32_t *ch)
{
    if (!open_ || dev != 42) return 0x1A;
    if (proto == 99) { strcpy(lasterr, "stub: protocol 99 is not supported"); return 0x03; }
    *ch = 7; qn = 0; cn = 0; return 0;
}
int32_t PassThruDisconnect(uint32_t ch) { return ch == 7 ? 0 : 0x02; }
int32_t PassThruWriteMsgs(uint32_t ch, PASSTHRU_MSG *msgs, uint32_t *num, uint32_t timeout)
{
    if (ch != 7) return 0x02;
    for (uint32_t i = 0; i < *num && qn < 16; i++)
    {
        PASSTHRU_MSG r = msgs[i];
        uint32_t id = id_of(&msgs[i]) + 8;
        r.Data[0] = id >> 24; r.Data[1] = id >> 16; r.Data[2] = id >> 8; r.Data[3] = id;
        if (r.DataSize > 4) r.Data[4] += 0x40;
        r.TxFlags = 0; r.RxStatus = 0; r.Timestamp = 1234; r.ExtraDataIndex = r.DataSize;
        queue[qn++] = r;
    }
    return 0;
}
int32_t PassThruReadMsgs(uint32_t ch, PASSTHRU_MSG *msgs, uint32_t *num, uint32_t timeout)
{
    if (ch != 7) return 0x02;
    if (qn == 0) { *num = 0; return 0x10; } /* ERR_BUFFER_EMPTY */
    uint32_t n = *num < (uint32_t)qn ? *num : (uint32_t)qn;
    memcpy(msgs, queue, n * sizeof(PASSTHRU_MSG));
    memmove(queue, queue + n, (qn - n) * sizeof(PASSTHRU_MSG)); qn -= n;
    *num = n; return 0;
}
int32_t PassThruStartMsgFilter(uint32_t ch, uint32_t type, PASSTHRU_MSG *mask, PASSTHRU_MSG *pattern, PASSTHRU_MSG *flow, uint32_t *id)
{
    filter_type = type; fc_mask = id_of(mask); fc_pattern = id_of(pattern); fc_flow = flow ? id_of(flow) : 0xFFFFFFFF;
    *id = 3; return 0;
}
int32_t PassThruStopMsgFilter(uint32_t ch, uint32_t id) { return id == 3 ? 0 : 0x16; }
int32_t PassThruIoctl(uint32_t ch, uint32_t ioctl, void *in, void *out)
{
    if (ioctl == 2) /* SET_CONFIG */
    {
        SCONFIG_LIST *l = (SCONFIG_LIST *)in;
        for (uint32_t i = 0; i < l->NumOfParams && cn < 16; i++) { cfg_param[cn] = l->ConfigPtr[i].Parameter; cfg_value[cn] = l->ConfigPtr[i].Value; cn++; }
        return 0;
    }
    if (ioctl == 3) { *(uint32_t *)out = 12345; return 0; } /* READ_VBATT */
    if (ioctl == 8) { qn = 0; return 0; }                    /* CLEAR_RX_BUFFER */
    return 0x0F;
}
int32_t PassThruReadVersion(uint32_t dev, char *fw, char *dll, char *api) { strcpy(fw, "STUB-FW 1.2"); strcpy(dll, "STUB-DLL 3.4"); strcpy(api, "04.04"); return 0; }
int32_t PassThruGetLastError(char *text) { strcpy(text, lasterr); return 0; }

uint32_t Stub_ConfigCount(void) { return cn; }
uint32_t Stub_ConfigParam(int i) { return cfg_param[i]; }
uint32_t Stub_ConfigValue(int i) { return cfg_value[i]; }
uint32_t Stub_Filter(int which) { return which == 0 ? filter_type : which == 1 ? fc_mask : which == 2 ? fc_pattern : fc_flow; }
