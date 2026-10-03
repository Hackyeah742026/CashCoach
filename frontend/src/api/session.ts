// The signed-in user (X-User-Id) and the backend's "today". Stored in localStorage;
// storage can be unavailable (private mode), so every access is guarded.

const USER_KEY = 'cashcoach.userId'
const AS_OF_KEY = 'cashcoach.asOf'

function read(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

function write(key: string, value: string | null) {
  try {
    if (value === null) localStorage.removeItem(key)
    else localStorage.setItem(key, value)
  } catch {
    // Not persisted; the session lasts until reload.
  }
}

let userId = read(USER_KEY)
let asOf = read(AS_OF_KEY)

export const getUserId = () => userId

export function setUserId(id: string) {
  userId = id
  write(USER_KEY, id)
}

/** Latest transaction date: "today" for all backend calculations. */
export const getAsOf = () => asOf

export function setAsOf(date: string | null) {
  asOf = date
  write(AS_OF_KEY, date)
}

export function clearSession() {
  userId = null
  asOf = null
  write(USER_KEY, null)
  write(AS_OF_KEY, null)
}
