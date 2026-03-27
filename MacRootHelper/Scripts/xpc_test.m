#import <Foundation/Foundation.h>
#import <xpc/xpc.h>

static xpc_connection_t gConnection = NULL;
static dispatch_queue_t gQueue = NULL;
static dispatch_semaphore_t gWait = NULL;
static NSString *gWaitRequestId = nil;
static NSDictionary *gFinalEvent = nil;

static NSString *JsonStringFromObject(id obj)
{
    if (obj == nil)
        return @"{}";
    NSData *data = [NSJSONSerialization dataWithJSONObject:obj options:0 error:nil];
    if (data == nil)
        return @"{}";
    return [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] ?: @"{}";
}

static NSDictionary *DictionaryFromJson(NSString *json)
{
    if (json.length == 0)
        return @{};
    NSData *data = [json dataUsingEncoding:NSUTF8StringEncoding];
    if (data == nil)
        return @{};
    id obj = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    return [obj isKindOfClass:[NSDictionary class]] ? obj : @{};
}

static void HandleEvent(xpc_object_t event)
{
    if (xpc_get_type(event) != XPC_TYPE_DICTIONARY)
        return;

    const char *json = xpc_dictionary_get_string(event, "json");
    NSString *jsonText = json ? [NSString stringWithUTF8String:json] : @"{}";
    NSDictionary *evt = DictionaryFromJson(jsonText);

    NSString *reqId = evt[@"requestId"] ?: @"";
    NSString *action = evt[@"action"] ?: @"";
    NSString *eventType = evt[@"eventType"] ?: @"";
    NSString *message = evt[@"message"] ?: @"";
    NSNumber *exitCode = evt[@"exitCode"] ?: @0;
    NSNumber *ok = evt[@"ok"] ?: @0;

    printf("[EVENT] action=%s type=%s ok=%s exit=%d msg=%s\n",
           action.UTF8String,
           eventType.UTF8String,
           ok.boolValue ? "true" : "false",
           exitCode.intValue,
           message.UTF8String);

    if (gWaitRequestId != nil &&
        [reqId isEqualToString:gWaitRequestId] &&
        ([eventType isEqualToString:@"completed"] || [eventType isEqualToString:@"failed"]))
    {
        gFinalEvent = evt;
        dispatch_semaphore_signal(gWait);
    }
}

static BOOL SendRequest(NSString *action, NSDictionary *payload, NSString *token, int timeoutSec, NSDictionary **outEvent)
{
    if (gConnection == NULL)
        return NO;

    NSString *requestId = [[NSUUID UUID] UUIDString];
    NSDictionary *req = @{
        @"requestId": requestId ?: @"",
        @"action": action ?: @"",
        @"payload": JsonStringFromObject(payload ?: @{}),
        @"timeoutSec": @(timeoutSec),
        @"source": @"xpc_test",
        @"token": token ?: @""
    };
    NSString *json = JsonStringFromObject(req);

    gWaitRequestId = requestId;
    gFinalEvent = nil;

    xpc_object_t msg = xpc_dictionary_create(NULL, NULL, 0);
    xpc_dictionary_set_string(msg, "json", json.UTF8String);
    xpc_connection_send_message(gConnection, msg);

    dispatch_time_t deadline = dispatch_time(DISPATCH_TIME_NOW, (int64_t)(timeoutSec * NSEC_PER_SEC));
    long result = dispatch_semaphore_wait(gWait, deadline);
    if (result != 0)
    {
        printf("[TIMEOUT] action=%s\n", action.UTF8String);
        if (outEvent != NULL)
            *outEvent = nil;
        return NO;
    }

    if (outEvent != NULL)
        *outEvent = gFinalEvent;
    return [gFinalEvent[@"ok"] respondsToSelector:@selector(boolValue)] ? [gFinalEvent[@"ok"] boolValue] : NO;
}

int main(int argc, const char * argv[])
{
    @autoreleasepool
    {
        NSString *token = @"unitool-default-token";
        if (argc > 1)
            token = [NSString stringWithUTF8String:argv[1]];

        gQueue = dispatch_queue_create("com.unitool.xpc_test", DISPATCH_QUEUE_SERIAL);
        gConnection = xpc_connection_create_mach_service("com.unitool.roothelper", gQueue, 0);
        if (gConnection == NULL)
        {
            printf("[ERROR] failed to create xpc connection\n");
            return 1;
        }

        xpc_connection_set_event_handler(gConnection, ^(xpc_object_t event) {
            HandleEvent(event);
        });
        xpc_connection_resume(gConnection);

        gWait = dispatch_semaphore_create(0);

        int failed = 0;
        NSDictionary *evt = nil;

        printf("== Root Helper XPC Test ==\n");

        if (!SendRequest(@"trust.refresh", @{ @"token": token }, token, 10, &evt))
        {
            printf("[FAIL] trust.refresh\n");
            failed++;
        }
        else
        {
            printf("[PASS] trust.refresh\n");
        }

        if (!SendRequest(@"helper.ping", @{}, token, 10, &evt))
        {
            printf("[FAIL] helper.ping\n");
            failed++;
        }
        else
        {
            printf("[PASS] helper.ping\n");
        }

        if (!SendRequest(@"helper.status", @{}, token, 10, &evt))
        {
            printf("[FAIL] helper.status\n");
            failed++;
        }
        else
        {
            printf("[PASS] helper.status\n");
        }

        if (!SendRequest(@"shell.exec", @{ @"command": @"id" }, token, 15, &evt))
        {
            printf("[FAIL] shell.exec id\n");
            failed++;
        }
        else
        {
            printf("[PASS] shell.exec id\n");
        }

        if (!SendRequest(@"shell.exec", @{ @"command": @"printf 'orig\\n' > /Users/Shared/UniTool/helper/hosts.test" }, token, 15, &evt))
        {
            printf("[FAIL] shell.exec seed hosts\n");
            failed++;
        }
        else
        {
            printf("[PASS] shell.exec seed hosts\n");
        }

        if (!SendRequest(@"hosts.update", @{ @"targetPath": @"/Users/Shared/UniTool/helper/hosts.test", @"content": @"line1\n" }, token, 15, &evt))
        {
            printf("[FAIL] hosts.update\n");
            failed++;
        }
        else
        {
            printf("[PASS] hosts.update\n");
        }

        if (!SendRequest(@"hosts.restore", @{ @"targetPath": @"/Users/Shared/UniTool/helper/hosts.test" }, token, 15, &evt))
        {
            printf("[FAIL] hosts.restore\n");
            failed++;
        }
        else
        {
            printf("[PASS] hosts.restore\n");
        }

        if (!SendRequest(@"shell.exec", @{ @"command": @"cat /Users/Shared/UniTool/helper/hosts.test" }, token, 15, &evt))
        {
            printf("[FAIL] shell.exec cat hosts\n");
            failed++;
        }
        else
        {
            printf("[PASS] shell.exec cat hosts\n");
        }

        printf("== Summary ==\n");
        printf("Failed: %d\n", failed);
        return failed == 0 ? 0 : 2;
    }
}
