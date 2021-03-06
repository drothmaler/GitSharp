﻿/*
 * Copyright (C) 2009, Robin Rosenberg
 *
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or
 * without modification, are permitted provided that the following
 * conditions are met:
 *
 * - Redistributions of source code must retain the above copyright
 *   notice, this list of conditions and the following disclaimer.
 *
 * - Redistributions in binary form must reproduce the above
 *   copyright notice, this list of conditions and the following
 *   disclaimer in the documentation and/or other materials provided
 *   with the distribution.
 *
 * - Neither the name of the Git Development Community nor the
 *   names of its contributors may be used to endorse or promote
 *   products derived from this software without specific prior
 *   written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES,
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
 * STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.IO;
using System.Threading;
using RefResult = GitSharp.RefUpdate.RefUpdateResult;

namespace GitSharp
{
	public class RefRename
	{
		private readonly RefUpdate _newToUpdate;
		private readonly RefUpdate _oldFromDelete;
		private RefResult _renameResult;

		public RefRename(RefUpdate toUpdate, RefUpdate fromUpdate)
		{
			_renameResult = RefResult.NotAttempted;
			_newToUpdate = toUpdate;
			_oldFromDelete = fromUpdate;
		}

		/// <summary>
		/// Gets the result of rename operation.
		/// </summary>
		public RefResult Result
		{
			get { return _renameResult; }
		}

		/// <summary>
		/// The result of the new ref update
		/// </summary>
		/// <returns></returns>
		/// <exception cref="IOException"></exception>
		public RefResult Rename()
		{
			Ref oldRef = _oldFromDelete.Repository.getRef(Constants.HEAD);
			bool renameHeadToo = oldRef != null && oldRef.Name == _oldFromDelete.Name;
			Repository db = _oldFromDelete.Repository;
			RefLogWriter.renameTo(db, _oldFromDelete, _newToUpdate);
			_newToUpdate.SetRefLogMessage(null, false);
			string tmpRefName = "RENAMED-REF.." + Thread.CurrentThread.ManagedThreadId;

			RefUpdate tmpUpdateRef = db.UpdateRef(tmpRefName);
			if (renameHeadToo)
			{
				try
				{
					_oldFromDelete.Repository.Link(Constants.HEAD, tmpRefName);
				}
				catch (IOException)
				{
					RefLogWriter.renameTo(db, _newToUpdate, _oldFromDelete);
					return _renameResult = RefResult.LockFailure;
				}
			}

			tmpUpdateRef.NewObjectId = _oldFromDelete.OldObjectId;
			tmpUpdateRef.IsForceUpdate = true;
			RefResult update = tmpUpdateRef.Update();
			if (update != RefResult.Forced && update != RefResult.New && update != RefResult.NoChange)
			{
				RefLogWriter.renameTo(db, _newToUpdate, _oldFromDelete);
				if (renameHeadToo)
				{
					_oldFromDelete.Repository.Link(Constants.HEAD, _oldFromDelete.Name);
				}

				return _renameResult = update;
			}

			_oldFromDelete.ExpectedOldObjectId = _oldFromDelete.OldObjectId;
			_oldFromDelete.IsForceUpdate = true;
			RefResult delete = _oldFromDelete.Delete();
			if (delete != RefResult.Forced)
			{
				if (db.getRef(_oldFromDelete.Name) != null)
				{
					RefLogWriter.renameTo(db, _newToUpdate, _oldFromDelete);
					if (renameHeadToo)
					{
						_oldFromDelete.Repository.Link(Constants.HEAD, _oldFromDelete.Name);
					}
				}
				return _renameResult = delete;
			}

			_newToUpdate.NewObjectId = tmpUpdateRef.NewObjectId;
			RefResult updateResult = _newToUpdate.Update();
			if (updateResult != RefResult.New)
			{
				RefLogWriter.renameTo(db, _newToUpdate, _oldFromDelete);
				if (renameHeadToo)
				{
					_oldFromDelete.Repository.Link(Constants.HEAD, _oldFromDelete.Name);
				}
				_oldFromDelete.ExpectedOldObjectId = null;
				_oldFromDelete.NewObjectId = _oldFromDelete.OldObjectId;
				_oldFromDelete.IsForceUpdate = true;
				_oldFromDelete.SetRefLogMessage(null, false);
				RefResult undelete = _oldFromDelete.Update();
				if (undelete != RefResult.New && undelete != RefResult.LockFailure)
				{
					return _renameResult = RefResult.IOFailure;
				}
				return _renameResult = RefResult.LockFailure;
			}

			if (renameHeadToo)
			{
				_oldFromDelete.Repository.Link(Constants.HEAD, _newToUpdate.Name);
			}
			else
			{
				db.OnRefsChanged();
			}

			RefLogWriter.append(this, _newToUpdate.Name, "Branch: renamed "
				+ Repository.ShortenRefName(_oldFromDelete.Name) + " to "
				+ Repository.ShortenRefName(_newToUpdate.Name));

			if (renameHeadToo)
			{
				RefLogWriter.append(this, Constants.HEAD, "Branch: renamed "
					+ Repository.ShortenRefName(_oldFromDelete.Name) + " to "
					+ Repository.ShortenRefName(_newToUpdate.Name));
			}

			return _renameResult = RefResult.Renamed;
		}

		public ObjectId ObjectId
		{
			get { return _oldFromDelete.OldObjectId; }
		}

		public Repository Repository
		{
			get { return _oldFromDelete.Repository; }
		}

		public PersonIdent RefLogIdent
		{
			get { return _newToUpdate.RefLogIdent; }
		}

		public string ToName
		{
			get { return _newToUpdate.Name; }
		}
	}
}