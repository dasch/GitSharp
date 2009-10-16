/*
 * Copyright (C) 2007, Dave Watson <dwatson@mimvista.com>
 * Copyright (C) 2007, Robin Rosenberg <robin.rosenberg@dewire.com>
 * Copyright (C) 2007, Shawn O. Pearce <spearce@spearce.org>
 * Copyright (C) 2008, Kevin Thompson <kevin.thompson@theautomaters.com>
 * Copyright (C) 2009, Henon <meinrad.recheis@gmail.com>
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GitSharp.Core.Exceptions;
using System.IO;
using GitSharp.Core.Util;

namespace GitSharp.Core
{
	/// <summary>
	/// Instances of this class represent a Commit object. It represents a snapshot
	/// in a Git repository, who created it and when.
	/// </summary>
	public class Commit : Treeish
	{
		private static readonly ObjectId[] EmptyObjectidList = new ObjectId[0];

		private byte[] _raw;
		private string _message;
		private PersonIdent _committer;
		private PersonIdent _author;
		private ObjectId _treeId;
		private Tree _treeEntry;

		///	<summary>
		/// Create an empty commit object. More information must be fed to this
		/// object to make it useful.
		/// </summary>
		/// <param name="db">
		/// The repository with which to associate it.
		/// </param>
		public Commit(Repository db)
			: this(db, EmptyObjectidList)
		{
		}

		///	<summary>
		/// Create a commit associated with these parents and associate it with a
		/// repository.
		/// </summary>
		/// <param name="db">
		///	The repository to which this commit object belongs.
		/// </param>
		///	<param name="parentIds">
		///	Id's of the parent(s).
		/// </param>
		public Commit(Repository db, ObjectId[] parentIds)
		{
			Repository = db;
			ParentIds = parentIds;
		}
		///	<summary>
		/// Create a commit object with the specified id and data from an existing
		/// commit object in a repository.
		/// </summary>
		/// <param name="db">
		/// The repository to which this commit object belongs.
		/// </param>
		/// <param name="id">Commit id.</param>
		/// <param name="raw">Raw commit object data.</param>
		public Commit(Repository db, ObjectId id, byte[] raw)
		{
			Repository = db;
			CommitId = id;
			_treeId = ObjectId.FromString(raw, 5);
			ParentIds = new ObjectId[1];
			int np = 0;
			int rawPtr = 46;
			while (true)
			{
				if (raw[rawPtr] != 'p') break;

				if (np == 0)
				{
					ParentIds[np++] = ObjectId.FromString(raw, rawPtr + 7);
				}
				else if (np == 1)
				{
					ParentIds = new[] { ParentIds[0], ObjectId.FromString(raw, rawPtr + 7) };
					np++;
				}
				else
				{
					if (ParentIds.Length <= np)
					{
						ObjectId[] old = ParentIds;
						ParentIds = new ObjectId[ParentIds.Length+32];
						for (int i=0; i<np; ++i)
						{
							ParentIds[i] = old[i];
						}
					}
					ParentIds[np++] = ObjectId.FromString(raw, rawPtr + 7);
				}
				rawPtr += 48;
			}

			if (np != ParentIds.Length)
			{
				ObjectId[] old = ParentIds;
				ParentIds = new ObjectId[np];
				for (int i = 0; i < np; ++i)
				{
					ParentIds[i] = old[i];
				}
			}
			else
			{
				if (np == 0)
				{
					ParentIds = EmptyObjectidList;
				}
			}

			_raw = raw;
		}

		#region Treeish Members

		public ObjectId TreeId
		{
			get { return _treeId; }
			set
			{
				if (_treeId == null || !_treeId.Equals(value))
				{
					_treeEntry = null;
				}
				_treeId = value;
			}
		}

		public Tree TreeEntry
		{
			get
			{
				if (_treeEntry == null)
				{
					_treeEntry = Repository.MapTree(TreeId);
					if (_treeEntry == null)
					{
						throw new MissingObjectException(TreeId, ObjectType.Tree);
					}
				}

				return _treeEntry;
			}
			set
			{
				_treeId = value.TreeId;
				_treeEntry = value;
			}
		}

		#endregion

		public ObjectId CommitId { get; private set; }
		public ObjectId[] ParentIds { get; set; }
		public Encoding Encoding { get; set; }
		public Repository Repository { get; private set; }

		public string Message
		{
			get
			{
				Decode();
				return _message;
			}
			set { _message = value; }
		}

		public PersonIdent Committer
		{
			get
			{
				Decode();
				return _committer;
			}
			set { _committer = value; }
		}


		public PersonIdent Author
		{
			get
			{
				Decode();
				return _author;
			}
			set { _author = value; }
		}

		private void Decode()
		{
			if (_raw == null) return;

			using (var decoder = new ObjectDecoder(_raw))
			{
				decoder.Read("tree");
				
				while (decoder.TryRead("parent") != null) { }
				string rawAuthor = decoder.Read("author");
				string rawCommitter = decoder.Read("committer");
				string rawEncoding = decoder.TryRead("encoding");

#warning This does not currently support custom encodings
				//byte[] readBuf = new byte[br.available()]; // in-memory stream so this is all bytes left
				//br.Read(readBuf);
				//int msgstart = readBuf.Length != 0 ? (readBuf[0] == '\n' ? 1 : 0) : 0;

				if (rawEncoding != null)
				{
					// TODO: this isn't reliable so we need to guess the encoding from the actual content
					throw new NotSupportedException("Custom Encoding is not currently supported.");
					//_author = new PersonIdent(new string(this.Encoding.GetBytes(rawAuthor), this.Encoding));
					//_committer = new PersonIdent(new string(rawCommitter.getBytes(), encoding.name()));
					//_message = new string(readBuf, msgstart, readBuf.Length - msgstart, encoding.name());
				}

				// TODO: use config setting / platform / ascii / iso-latin
				_author = new PersonIdent(rawAuthor);
				_committer = new PersonIdent(rawCommitter);
				//_message = new string(readBuf, msgstart, readBuf.Length - msgstart);
				_message = decoder.ReadToEnd();
			}

			_raw = null;
		}

		///	<summary>
		/// Persist this commit object
		/// </summary>
		/// <exception cref="IOException"></exception>
		public void Save() // [henon] was Commit() in java, but c# won't allow it
		{
			if (CommitId != null)
			{
				throw new InvalidOperationException("exists " + CommitId);
			}

			CommitId = new ObjectWriter(Repository).WriteCommit(this);
		}

		public override string ToString()
		{
			return "Commit[" + CommitId + " " + Author + "]";
		}
	}
}