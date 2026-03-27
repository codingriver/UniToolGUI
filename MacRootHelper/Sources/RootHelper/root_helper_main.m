#import <Foundation/Foundation.h>
#import <xpc/xpc.h>
#import <CommonCrypto/CommonDigest.h>
#import <libproc.h>

static NSString *const kHelperLabel = @"com.unitool.roothelper";
static NSString *const kRuntimeRoot = @"/Users/Shared/UniTool";
static NSString *const kHelperStateDir = @"/Users/Shared/UniTool/helper";
static NSString *const kHelperLogPath = @"/Users/Shared/UniTool/logs/helper.log";
static NSString *const kTrustPath = @"/Users/Shared/UniTool/helper/trust.json";
static NSString *const kBackupDir = @"/Users/Shared/UniTool/helper/backups";
static NSString *const kDefaultTrustToken = @"unitool-default-token";

@interface HelperRuntime : NSObject
@end

@implementation HelperRuntime

+ (NSFileManager *)fm
{
    return [NSFileManager defaultManager];
}

+ (void)ensureDirectories
{
    NSArray<NSString *> *dirs = @[
        kRuntimeRoot,
        kHelperStateDir,
        [kHelperLogPath stringByDeletingLastPathComponent],
        kBackupDir
    ];

    for (NSString *dir in dirs)
    {
        [[self fm] createDirectoryAtPath:dir
             withIntermediateDirectories:YES
                              attributes:@{ NSFilePosixPermissions : @0755 }
                                   error:nil];
    }
}

+ (void)log:(NSString *)message
{
    [self ensureDirectories];
    NSString *time = [[NSDate date] description];
    NSString *line = [NSString stringWithFormat:@"[%@] %@\n", time, message ?: @""];
    NSFileHandle *handle = [NSFileHandle fileHandleForWritingAtPath:kHelperLogPath];
    if (handle == nil)
    {
        [line writeToFile:kHelperLogPath atomically:YES encoding:NSUTF8StringEncoding error:nil];
        return;
    }

    @try
    {
        [handle seekToEndOfFile];
        [handle writeData:[line dataUsingEncoding:NSUTF8StringEncoding]];
        [handle closeFile];
    }
    @catch (__unused NSException *ex)
    {
    }
}

+ (NSString *)jsonStringFromObject:(id)obj
{
    if (obj == nil)
        return @"{}";

    NSData *data = [NSJSONSerialization dataWithJSONObject:obj options:0 error:nil];
    if (data == nil)
        return @"{}";
    return [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] ?: @"{}";
}

+ (NSDictionary *)dictionaryFromJsonString:(NSString *)json
{
    if (json.length == 0)
        return @{};

    NSData *data = [json dataUsingEncoding:NSUTF8StringEncoding];
    if (data == nil)
        return @{};

    id obj = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
    return [obj isKindOfClass:[NSDictionary class]] ? obj : @{};
}

+ (NSString *)readString:(NSString *)path
{
    return [NSString stringWithContentsOfFile:path encoding:NSUTF8StringEncoding error:nil];
}

+ (BOOL)writeString:(NSString *)value toPath:(NSString *)path mode:(NSNumber *)mode
{
    if (value == nil)
        value = @"";
    NSError *error = nil;
    BOOL ok = [value writeToFile:path atomically:YES encoding:NSUTF8StringEncoding error:&error];
    if (!ok)
    {
        [self log:[NSString stringWithFormat:@"[Helper] 写文件失败 %@ error=%@", path, error.localizedDescription ?: @"unknown"]];
        return NO;
    }

    if (mode != nil)
        [[NSFileManager defaultManager] setAttributes:@{ NSFilePosixPermissions : mode } ofItemAtPath:path error:nil];
    return YES;
}

+ (NSString *)sha256ForFile:(NSString *)path
{
    NSFileHandle *handle = [NSFileHandle fileHandleForReadingAtPath:path];
    if (handle == nil)
        return nil;

    CC_SHA256_CTX ctx;
    CC_SHA256_Init(&ctx);
    while (true)
    {
        @autoreleasepool
        {
            NSData *chunk = [handle readDataOfLength:65536];
            if (chunk.length == 0)
                break;
            CC_SHA256_Update(&ctx, chunk.bytes, (CC_LONG)chunk.length);
        }
    }
    [handle closeFile];

    unsigned char digest[CC_SHA256_DIGEST_LENGTH];
    CC_SHA256_Final(digest, &ctx);

    NSMutableString *output = [NSMutableString stringWithCapacity:CC_SHA256_DIGEST_LENGTH * 2];
    for (int i = 0; i < CC_SHA256_DIGEST_LENGTH; i++)
        [output appendFormat:@"%02x", digest[i]];
    return output;
}

+ (NSString *)pathForPid:(pid_t)pid
{
    if (pid <= 0)
        return nil;

    char buffer[PROC_PIDPATHINFO_MAXSIZE];
    int ret = proc_pidpath(pid, buffer, sizeof(buffer));
    if (ret <= 0)
        return nil;
    return [NSString stringWithUTF8String:buffer];
}

+ (NSDictionary *)trustInfo
{
    NSDictionary *dict = [self dictionaryFromJsonString:[self readString:kTrustPath]];
    return dict ?: @{};
}

+ (BOOL)isConnectionTrusted:(xpc_connection_t)peer
               providedToken:(NSString *)providedToken
                  callerPath:(NSString **)outPath
                 callerHash:(NSString **)outHash
{
    pid_t pid = xpc_connection_get_pid(peer);
    NSString *path = [self pathForPid:pid];
    NSString *hash = path.length > 0 ? [self sha256ForFile:path] : nil;
    if (outPath != NULL) *outPath = path;
    if (outHash != NULL) *outHash = hash;

    NSDictionary *trust = [self trustInfo];
    NSString *expectedToken = trust[@"token"];
    if (expectedToken.length == 0)
        expectedToken = kDefaultTrustToken;

    if (providedToken.length == 0)
        return NO;

    return [expectedToken isEqualToString:providedToken];
}

+ (void)sendEventToPeer:(xpc_connection_t)peer
              requestId:(NSString *)requestId
                 action:(NSString *)action
              eventType:(NSString *)eventType
                     ok:(BOOL)ok
               exitCode:(NSInteger)exitCode
                message:(NSString *)message
            payloadJson:(NSString *)payloadJson
{
    NSDictionary *obj = @{
        @"requestId" : requestId ?: @"",
        @"action" : action ?: @"",
        @"eventType" : eventType ?: @"",
        @"ok" : @(ok),
        @"exitCode" : @(exitCode),
        @"message" : message ?: @"",
        @"payloadJson" : payloadJson ?: @"{}"
    };

    NSString *json = [self jsonStringFromObject:obj];
    xpc_object_t msg = xpc_dictionary_create(NULL, NULL, 0);
    xpc_dictionary_set_string(msg, "json", json.UTF8String);
    xpc_connection_send_message(peer, msg);
}

+ (NSString *)latestBackupForTarget:(NSString *)targetPath
{
    NSArray<NSString *> *files = [[self fm] contentsOfDirectoryAtPath:kBackupDir error:nil];
    if (files.count == 0)
        return nil;

    NSString *prefix = [[targetPath lastPathComponent] stringByAppendingString:@"."];
    NSString *latest = nil;
    for (NSString *file in files)
    {
        if (![file hasPrefix:prefix] || ![file hasSuffix:@".bak"])
            continue;
        if (latest == nil || [file compare:latest options:NSNumericSearch] == NSOrderedDescending)
            latest = file;
    }

    return latest == nil ? nil : [kBackupDir stringByAppendingPathComponent:latest];
}

+ (NSString *)createBackupForTarget:(NSString *)targetPath
{
    [self ensureDirectories];
    NSString *timestamp = [NSString stringWithFormat:@"%.0f", [[NSDate date] timeIntervalSince1970]];
    NSString *backupName = [NSString stringWithFormat:@"%@.%@.bak", [targetPath lastPathComponent], timestamp];
    NSString *backupPath = [kBackupDir stringByAppendingPathComponent:backupName];
    NSError *error = nil;
    if ([[self fm] fileExistsAtPath:targetPath])
    {
        if (![[self fm] copyItemAtPath:targetPath toPath:backupPath error:&error])
        {
            [self log:[NSString stringWithFormat:@"[Helper] 备份失败 %@ -> %@ error=%@", targetPath, backupPath, error.localizedDescription ?: @"unknown"]];
            return nil;
        }
    }
    return backupPath;
}

+ (BOOL)writeHostsContent:(NSString *)content targetPath:(NSString *)targetPath backupOut:(NSString **)backupOut errorMessage:(NSString **)errorMessage
{
    NSString *backupPath = [self createBackupForTarget:targetPath];
    if (backupOut != NULL) *backupOut = backupPath;

    NSString *tempPath = [kHelperStateDir stringByAppendingPathComponent:@"hosts.tmp"];
    NSError *error = nil;
    BOOL ok = [content writeToFile:tempPath atomically:YES encoding:NSUTF8StringEncoding error:&error];
    if (!ok)
    {
        if (errorMessage != NULL) *errorMessage = error.localizedDescription ?: @"临时文件写入失败";
        return NO;
    }

    [[NSFileManager defaultManager] setAttributes:@{ NSFilePosixPermissions : @0644 } ofItemAtPath:tempPath error:nil];
    if ([[self fm] fileExistsAtPath:targetPath] && ![[self fm] removeItemAtPath:targetPath error:nil])
    {
        if (errorMessage != NULL) *errorMessage = @"目标 hosts 无法覆盖";
        return NO;
    }

    ok = [[self fm] moveItemAtPath:tempPath toPath:targetPath error:&error];
    if (!ok)
    {
        if (errorMessage != NULL) *errorMessage = error.localizedDescription ?: @"hosts 覆盖失败";
        return NO;
    }
    [[NSFileManager defaultManager] setAttributes:@{ NSFilePosixPermissions : @0644 } ofItemAtPath:targetPath error:nil];
    return YES;
}

+ (void)runTaskForPeer:(xpc_connection_t)peer requestId:(NSString *)requestId action:(NSString *)action command:(NSString *)command timeout:(NSTimeInterval)timeout callerPath:(NSString *)callerPath callerHash:(NSString *)callerHash
{
    [self log:[NSString stringWithFormat:@"[Helper] shell.exec requestId=%@ caller=%@ sha256=%@ cmd=%@",
               requestId ?: @"", callerPath ?: @"", callerHash ?: @"", command ?: @""]];

    dispatch_async(dispatch_get_global_queue(QOS_CLASS_UTILITY, 0), ^{
        NSTask *task = [[NSTask alloc] init];
        task.launchPath = @"/bin/zsh";
        task.arguments = @[ @"-lc", command ?: @"" ];

        NSPipe *stdoutPipe = [NSPipe pipe];
        NSPipe *stderrPipe = [NSPipe pipe];
        task.standardOutput = stdoutPipe;
        task.standardError = stderrPipe;

        NSFileHandle *stdoutRead = stdoutPipe.fileHandleForReading;
        NSFileHandle *stderrRead = stderrPipe.fileHandleForReading;

        // Guard against duplicate terminal events (timeout race vs normal exit)
        __block BOOL finalEventSent = NO;
        dispatch_queue_t guardQueue = dispatch_queue_create("com.unitool.taskguard", DISPATCH_QUEUE_SERIAL);

        void (^sendFinal)(BOOL, int, NSString *) = ^(BOOL ok, int exitCode, NSString *msg) {
            dispatch_sync(guardQueue, ^{
                if (finalEventSent) return;
                finalEventSent = YES;
                [self sendEventToPeer:peer
                            requestId:requestId
                               action:action
                            eventType:(ok ? @"completed" : @"failed")
                                   ok:ok
                             exitCode:exitCode
                              message:msg ?: @""
                          payloadJson:@"{}"];
            });
        };

        [self sendEventToPeer:peer requestId:requestId action:action eventType:@"progress" ok:YES exitCode:0 message:@"shell.exec started" payloadJson:@"{}"];

        stdoutRead.readabilityHandler = ^(NSFileHandle *handle) {
            NSData *data = [handle availableData];
            if (data.length == 0) return;
            NSString *text = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] ?: @"";
            [self sendEventToPeer:peer requestId:requestId action:action eventType:@"stdout" ok:YES exitCode:0 message:text payloadJson:@"{}"];
        };

        stderrRead.readabilityHandler = ^(NSFileHandle *handle) {
            NSData *data = [handle availableData];
            if (data.length == 0) return;
            NSString *text = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] ?: @"";
            [self sendEventToPeer:peer requestId:requestId action:action eventType:@"stderr" ok:NO exitCode:0 message:text payloadJson:@"{}"];
        };

        @try
        {
            [task launch];
        }
        @catch (NSException *exception)
        {
            NSString *msg = [NSString stringWithFormat:@"命令启动失败: %@", exception.reason ?: @"unknown"];
            [self log:[NSString stringWithFormat:@"[Helper] %@", msg]];
            sendFinal(NO, -1, msg);
            return;
        }

        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(timeout * NSEC_PER_SEC)),
                       dispatch_get_global_queue(QOS_CLASS_UTILITY, 0), ^{
            if (task.isRunning)
            {
                [task terminate];
                sendFinal(NO, 124, @"命令执行超时");
            }
        });

        [task waitUntilExit];
        stdoutRead.readabilityHandler = nil;
        stderrRead.readabilityHandler = nil;

        int code = task.terminationStatus;
        NSString *message = code == 0 ? @"命令执行完成" : [NSString stringWithFormat:@"命令退出 code=%d", code];
        [self log:[NSString stringWithFormat:@"[Helper] shell.exec requestId=%@ exit=%d", requestId ?: @"", code]];
        sendFinal(code == 0, code, message);
    });
}

+ (void)handleRequestOnPeer:(xpc_connection_t)peer request:(NSDictionary *)request
{
    NSString *requestId = request[@"requestId"] ?: @"";
    NSString *action = request[@"action"] ?: @"";
    NSString *payloadJson = request[@"payload"] ?: @"{}";
    NSDictionary *payload = [self dictionaryFromJsonString:payloadJson];
    NSString *token = request[@"token"] ?: payload[@"token"] ?: @"";
    NSInteger timeoutSec = [request[@"timeoutSec"] respondsToSelector:@selector(integerValue)] ? [request[@"timeoutSec"] integerValue] : 60;
    timeoutSec = MAX(1, MIN(timeoutSec, 600));

    NSString *callerPath = nil;
    NSString *callerHash = nil;
    if ([action isEqualToString:@"trust.refresh"])
    {
        NSString *finalToken = (token.length > 0) ? token : kDefaultTrustToken;
        NSDictionary *trust = @{ @"token" : finalToken };
        BOOL ok = [self writeString:[self jsonStringFromObject:trust] toPath:kTrustPath mode:@0644];
        [self sendEventToPeer:peer
                    requestId:requestId
                       action:action
                    eventType:(ok ? @"completed" : @"failed")
                           ok:ok
                     exitCode:(ok ? 0 : 1)
                      message:(ok ? @"信任信息已刷新" : @"信任信息写入失败")
                  payloadJson:@"{}"];
        return;
    }

    if (![self isConnectionTrusted:peer providedToken:token callerPath:&callerPath callerHash:&callerHash])
    {
        [self log:[NSString stringWithFormat:@"[Helper] 拒绝未信任调用 action=%@ caller=%@ sha256=%@",
                   action ?: @"", callerPath ?: @"", callerHash ?: @""]];
        [self sendEventToPeer:peer requestId:requestId action:action eventType:@"failed" ok:NO exitCode:403 message:@"调用方未通过信任校验，请执行刷新信任" payloadJson:@"{}"];
        return;
    }

    [self sendEventToPeer:peer requestId:requestId action:action eventType:@"accepted" ok:YES exitCode:0 message:@"helper 已接收请求" payloadJson:@"{}"];

    if ([action isEqualToString:@"helper.ping"])
    {
        [self sendEventToPeer:peer requestId:requestId action:action eventType:@"completed" ok:YES exitCode:0 message:@"pong"
                  payloadJson:[self jsonStringFromObject:@{ @"label" : kHelperLabel, @"pid" : @((int)getpid()) }]];
        return;
    }

    if ([action isEqualToString:@"helper.status"])
    {
        NSDictionary *status = @{
            @"label" : kHelperLabel,
            @"pid" : @((int)getpid()),
            @"trustPath" : kTrustPath,
            @"logPath" : kHelperLogPath,
            @"backupDir" : kBackupDir
        };
        [self sendEventToPeer:peer requestId:requestId action:action eventType:@"completed" ok:YES exitCode:0 message:@"helper 运行中"
                  payloadJson:[self jsonStringFromObject:status]];
        return;
    }

    if ([action isEqualToString:@"hosts.update"])
    {
        NSString *targetPath = payload[@"targetPath"] ?: @"/etc/hosts";
        NSString *content = payload[@"content"] ?: @"";
        NSString *backupPath = nil;
        NSString *errorMessage = nil;
        BOOL ok = [self writeHostsContent:content targetPath:targetPath backupOut:&backupPath errorMessage:&errorMessage];
        NSDictionary *payloadObj = @{
            @"targetPath" : targetPath ?: @"",
            @"backupPath" : backupPath ?: @""
        };
        [self sendEventToPeer:peer
                    requestId:requestId
                       action:action
                    eventType:(ok ? @"completed" : @"failed")
                           ok:ok
                     exitCode:(ok ? 0 : 1)
                      message:(ok ? @"hosts 更新成功" : (errorMessage ?: @"hosts 更新失败"))
                  payloadJson:[self jsonStringFromObject:payloadObj]];
        return;
    }

    if ([action isEqualToString:@"hosts.restore"])
    {
        NSString *targetPath = payload[@"targetPath"] ?: @"/etc/hosts";
        NSString *backupPath = [self latestBackupForTarget:targetPath];
        NSError *error = nil;
        BOOL ok = NO;
        if (backupPath.length > 0)
        {
            if ([[self fm] fileExistsAtPath:targetPath])
                [[self fm] removeItemAtPath:targetPath error:nil];
            ok = [[self fm] copyItemAtPath:backupPath toPath:targetPath error:&error];
        }
        [self sendEventToPeer:peer
                    requestId:requestId
                       action:action
                    eventType:(ok ? @"completed" : @"failed")
                           ok:ok
                     exitCode:(ok ? 0 : 1)
                      message:(ok ? @"hosts 已恢复" : (error.localizedDescription ?: @"未找到可恢复备份"))
                  payloadJson:[self jsonStringFromObject:@{ @"targetPath" : targetPath ?: @"", @"backupPath" : backupPath ?: @"" }]];
        return;
    }

    if ([action isEqualToString:@"shell.exec"])
    {
        NSString *command = payload[@"command"] ?: @"";
        [self runTaskForPeer:peer requestId:requestId action:action command:command timeout:(NSTimeInterval)timeoutSec callerPath:callerPath callerHash:callerHash];
        return;
    }

    [self sendEventToPeer:peer requestId:requestId action:action eventType:@"failed" ok:NO exitCode:404 message:@"未知 action" payloadJson:@"{}"];
}

@end

static void handle_peer_event(xpc_connection_t peer, xpc_object_t event)
{
    xpc_type_t type = xpc_get_type(event);
    if (type == XPC_TYPE_DICTIONARY)
    {
        const char *json = xpc_dictionary_get_string(event, "json");
        NSString *jsonText = json ? [NSString stringWithUTF8String:json] : @"{}";
        NSDictionary *request = [HelperRuntime dictionaryFromJsonString:jsonText];
        NSString *action = request[@"action"] ?: @"";
        NSString *requestId = request[@"requestId"] ?: @"";
        [HelperRuntime log:[NSString stringWithFormat:@"[Helper] recv requestId=%@ action=%@ json=%@",
                            requestId ?: @"", action ?: @"", jsonText ?: @""]];
        [HelperRuntime handleRequestOnPeer:peer request:request];
        return;
    }

    if (type == XPC_TYPE_ERROR)
    {
        [HelperRuntime log:[NSString stringWithFormat:@"[Helper] peer error: %s", xpc_dictionary_get_string(event, XPC_ERROR_KEY_DESCRIPTION)]];
    }
}

static void peer_handler(xpc_connection_t peer)
{
    xpc_connection_set_event_handler(peer, ^(xpc_object_t event) {
        handle_peer_event(peer, event);
    });
    xpc_connection_resume(peer);
}

int main(int argc, const char * argv[])
{
    @autoreleasepool
    {
        [HelperRuntime ensureDirectories];
        [HelperRuntime log:[NSString stringWithFormat:@"[Helper] %@ 启动 argc=%d", kHelperLabel, argc]];
        xpc_connection_t listener = xpc_connection_create_mach_service(kHelperLabel.UTF8String,
                                                                       dispatch_get_main_queue(),
                                                                       XPC_CONNECTION_MACH_SERVICE_LISTENER);
        if (listener == NULL)
        {
            [HelperRuntime log:@"[Helper] xpc listener 创建失败"];
            return 1;
        }

        xpc_connection_set_event_handler(listener, ^(xpc_object_t event) {
            xpc_type_t type = xpc_get_type(event);
            if (type == XPC_TYPE_CONNECTION)
            {
                peer_handler((xpc_connection_t)event);
                return;
            }

            if (type == XPC_TYPE_ERROR)
            {
                const char *desc = xpc_dictionary_get_string(event, XPC_ERROR_KEY_DESCRIPTION);
                [HelperRuntime log:[NSString stringWithFormat:@"[Helper] listener error: %s", desc ?: "unknown"]];
            }
        });

        xpc_connection_resume(listener);
        dispatch_main();
    }
    return 0;
}
