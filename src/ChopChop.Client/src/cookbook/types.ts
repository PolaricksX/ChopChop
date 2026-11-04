export type Folder = {
  id: string
  name: string
  createdAtUtc: string
}

export type SavedRecipe = {
  folderId: string
  recipeId: string
  recipeVersion: number
  savedAtUtc: string
}
