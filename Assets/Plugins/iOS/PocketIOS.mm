#import <Foundation/Foundation.h>
#import <Security/Security.h>
#import <UIKit/UIKit.h>
#include "UnityAppController.h"
#include <stdlib.h>
#include <string.h>

static NSString *const PocketCredentialService = @"com.openfusion.pocket.credentials";
static NSMutableDictionary *CredentialQuery(const char *identity) {
    if (!identity) return nil;
    NSString *account = [NSString stringWithUTF8String:identity];
    if (!account.length) return nil;
    return [@{(__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
              (__bridge id)kSecAttrService: PocketCredentialService,
              (__bridge id)kSecAttrAccount: account,
              (__bridge id)kSecAttrSynchronizable: @NO} mutableCopy];
}
extern "C" {
int PocketIOS_BuildNumber() { return (int)[[NSBundle.mainBundle objectForInfoDictionaryKey:@"CFBundleVersion"] integerValue]; }
void PocketIOS_ShareFile(const char *path) {
    NSString *file = path ? [[NSString stringWithUTF8String:path] stringByResolvingSymlinksInPath] : nil;
    NSString *caches = [NSSearchPathForDirectoriesInDomains(NSCachesDirectory, NSUserDomainMask, YES).firstObject stringByResolvingSymlinksInPath];
    NSString *temp = [NSTemporaryDirectory() stringByResolvingSymlinksInPath];
    if (!file.length || ![file.pathExtension.lowercaseString isEqualToString:@"ipa"] ||
        (! [file hasPrefix:[caches stringByAppendingString:@"/"]] && ![file hasPrefix:[temp stringByAppendingString:@"/"]]) ||
        ![NSFileManager.defaultManager fileExistsAtPath:file]) return;
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *controller = UnityGetGLViewController();
        while (controller.presentedViewController) controller = controller.presentedViewController;
        if (!controller.view.window) return;
        UIActivityViewController *share = [[UIActivityViewController alloc] initWithActivityItems:@[[NSURL fileURLWithPath:file]] applicationActivities:nil];
        share.popoverPresentationController.sourceView = controller.view;
        share.popoverPresentationController.sourceRect = CGRectMake(CGRectGetMidX(controller.view.bounds), CGRectGetMidY(controller.view.bounds), 1, 1);
        [controller presentViewController:share animated:YES completion:nil];
    });
}
int PocketIOS_Save(const char *identity, const char *password) {
    @autoreleasepool {
        NSMutableDictionary *query = CredentialQuery(identity);
        if (!query || !password) return (int)errSecParam;
        NSString *value = [NSString stringWithUTF8String:password];
        if (!value) return (int)errSecParam;
        NSDictionary *attributes = @{(__bridge id)kSecValueData: [value dataUsingEncoding:NSUTF8StringEncoding],
            (__bridge id)kSecAttrAccessible: (__bridge id)kSecAttrAccessibleWhenUnlockedThisDeviceOnly};
        OSStatus result = SecItemUpdate((__bridge CFDictionaryRef)query, (__bridge CFDictionaryRef)attributes);
        if (result == errSecItemNotFound) {
            [query addEntriesFromDictionary:attributes]; result = SecItemAdd((__bridge CFDictionaryRef)query, NULL);
        }
        return (int)result;
    }
}
char *PocketIOS_Load(const char *identity) {
    @autoreleasepool {
        NSMutableDictionary *query = CredentialQuery(identity);
        if (!query) return NULL;
        query[(__bridge id)kSecReturnData] = @YES; query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
        CFTypeRef item = NULL;
        OSStatus result = SecItemCopyMatching((__bridge CFDictionaryRef)query, &item);
        if (result != errSecSuccess || !item) { if (item) CFRelease(item); return NULL; }
        NSData *data = CFBridgingRelease(item);
        NSString *value = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
        return value ? strdup(value.UTF8String) : NULL;
    }
}
int PocketIOS_Forget(const char *identity) {
    @autoreleasepool { NSMutableDictionary *query = CredentialQuery(identity); return query ? (int)SecItemDelete((__bridge CFDictionaryRef)query) : (int)errSecParam; }
}
int PocketIOS_ForgetAll() {
    @autoreleasepool {
        // Deliberately scoped to this app's service; never delete the whole keychain.
        NSDictionary *query = @{(__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
            (__bridge id)kSecAttrService: PocketCredentialService, (__bridge id)kSecAttrSynchronizable: @NO};
        return (int)SecItemDelete((__bridge CFDictionaryRef)query);
    }
}
void PocketIOS_Free(void *text) { free(text); }
void PocketIOS_Share(const char *report) {
    NSString *text = report ? [NSString stringWithUTF8String:report] : nil;
    if (!text.length) return;
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *controller = UnityGetGLViewController();
        while (controller.presentedViewController) controller = controller.presentedViewController;
        if (!controller.view.window) return;
        UIActivityViewController *share = [[UIActivityViewController alloc] initWithActivityItems:@[text] applicationActivities:nil];
        share.popoverPresentationController.sourceView = controller.view;
        share.popoverPresentationController.sourceRect = CGRectMake(CGRectGetMidX(controller.view.bounds), CGRectGetMidY(controller.view.bounds), 1, 1);
        [controller presentViewController:share animated:YES completion:nil];
    });
}
}
