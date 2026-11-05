export type UserProfile = {
  id: string
  username: string
  email: string
  displayName: string | null
  createdAt: string
  updatedAt: string
  version: string
}

export type UserProfileInput = {
  username: string
  email: string
  displayName: string | null
}
