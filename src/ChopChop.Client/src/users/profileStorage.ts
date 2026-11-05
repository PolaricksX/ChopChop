export function userProfileStorageKey(subject: string) {
  return `chopchop.user-profile.id:${subject}`
}

export function userProfileDeletedStorageKey(subject: string) {
  return `chopchop.user-profile.deleted:${subject}`
}
