#import <Foundation/Foundation.h>
#import <xpc/xpc.h>

typedef void (*UniToolXpcCallback)(const char *json);

static NSString *const kHelperMachService = @"com.unitool.roothelper";
static xpc_connection_t gConnection = NULL;
static UniToolXpcCallback gCallback = NULL;
static dispatch_queue_t gQueue = NULL;

static void EmitJson(NSString *json)
{
    if (gCallback == NULL || json.length == 0)
        return;
    gCallback(json.UTF8String);
}

static NSString *MakeEventJson(NSString *action, NSString *eventType, BOOL ok, NSInteger exitCode, NSString *message)
{
    NSDictionary *obj = @{
        @"requestId" : @"",
        @"action" : action ?: @"",
        @"eventType" : eventType ?: @"",
        @"ok" : @(ok),
        @"exitCode" : @(exitCode),
        @"message" : message ?: @"",
        @"payloadJson" : @"{}"
    };
    NSData *data = [NSJSONSerialization dataWithJSONObject:obj options:0 error:nil];
    return [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] ?: @"{}";
}

__attribute__((visibility("default")))
void UniToolXpc_SetCallback(UniToolXpcCallback callback)
{
    gCallback = callback;
}

__attribute__((visibility("default")))
int UniToolXpc_Connect(void)
{
    if (gConnection != NULL)
        return 1;

    gQueue = dispatch_queue_create("com.unitool.xpcbridge", DISPATCH_QUEUE_SERIAL);
    gConnection = xpc_connection_create_mach_service(kHelperMachService.UTF8String, gQueue, 0);
    if (gConnection == NULL)
        return 0;

    xpc_connection_set_event_handler(gConnection, ^(xpc_object_t event) {
        xpc_type_t type = xpc_get_type(event);
        if (type == XPC_TYPE_DICTIONARY)
        {
            const char *json = xpc_dictionary_get_string(event, "json");
            if (json != NULL)
                EmitJson([NSString stringWithUTF8String:json]);
            return;
        }

        if (type == XPC_TYPE_ERROR)
        {
            const char *desc = xpc_dictionary_get_string(event, XPC_ERROR_KEY_DESCRIPTION);
            NSString *message = desc ? [NSString stringWithUTF8String:desc] : @"xpc connection error";
            EmitJson(MakeEventJson(@"connection", @"connection_error", NO, -1, message));
        }
    });
    xpc_connection_resume(gConnection);
    EmitJson(MakeEventJson(@"connection", @"connection_opened", YES, 0, @"xpc connected"));
    return 1;
}

__attribute__((visibility("default")))
void UniToolXpc_Disconnect(void)
{
    if (gConnection != NULL)
    {
        xpc_connection_cancel(gConnection);
        gConnection = NULL;
    }
    EmitJson(MakeEventJson(@"connection", @"connection_closed", YES, 0, @"xpc disconnected"));
}

__attribute__((visibility("default")))
int UniToolXpc_SendJson(const char *json)
{
    if (gConnection == NULL || json == NULL)
        return 0;

    xpc_object_t msg = xpc_dictionary_create(NULL, NULL, 0);
    xpc_dictionary_set_string(msg, "json", json);
    xpc_connection_send_message(gConnection, msg);
    return 1;
}
